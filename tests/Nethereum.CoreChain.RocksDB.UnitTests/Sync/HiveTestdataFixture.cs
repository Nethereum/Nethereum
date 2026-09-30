using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public static class HiveTestdataFixture
    {
        public const string TestdataRelativePath =
            "go-ethereum/cmd/devp2p/internal/ethtest/testdata";

        private static readonly Lazy<string> _testdataDir = new(LocateTestdataDir);
        private static readonly Lazy<IReadOnlyList<BlockBundle>> _chain = new(LoadChain);
        private static readonly Lazy<JObject> _genesis = new(LoadGenesis);
        private static readonly Lazy<IReadOnlyDictionary<string, JObject>> _genesisAllocRaw =
            new(LoadGenesisAllocRaw);
        private static readonly Lazy<HiveForkSchedule> _forkSchedule =
            new(() => HiveForkSchedule.Load(TestdataDir, HardforkConfigFactory));

        public static string TestdataDir =>
            _testdataDir.Value ?? throw new DirectoryNotFoundException(TestdataNotFoundMessage());

        public static bool IsAvailable => _testdataDir.Value != null;

        public static IReadOnlyList<BlockBundle> Chain => _chain.Value;

        public static IReadOnlyDictionary<string, JObject> GenesisAllocRaw => _genesisAllocRaw.Value;

        public static IChainActivations ChainActivations => _forkSchedule.Value;

        public static BigInteger ChainId => _genesis.Value["config"]["chainId"].ToObject<BigInteger>();

        public static Func<HardforkName, HardforkConfig> HardforkConfigFactory { get; } =
            f => DefaultMainnetHardforkRegistry.Instance.Get(f);

        public static Func<HardforkName, ChainConfig> ChainConfigFactory { get; } =
            f => new ChainConfig
            {
                ChainId = ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = f.ToString().ToLowerInvariant()
            };

        public static async System.Threading.Tasks.Task PopulateGenesisAsync(IStateStore stateStore)
        {
            var keccak = new Sha3Keccack();
            foreach (var kv in GenesisAllocRaw)
            {
                var addr = kv.Key.StartsWith("0x") ? kv.Key : "0x" + kv.Key;
                var entry = kv.Value;
                var balance = entry["balance"] != null
                    ? new HexBigInteger(entry["balance"].ToString()).Value
                    : BigInteger.Zero;
                ulong nonce = 0;
                if (entry["nonce"] != null)
                {
                    var nVal = new HexBigInteger(entry["nonce"].ToString()).Value;
                    nonce = nVal.IsZero ? 0UL : (ulong)nVal;
                }
                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (entry["code"] != null)
                {
                    var code = entry["code"].ToString().HexToByteArray();
                    codeHash = keccak.CalculateHash(code);
                    await stateStore.SaveCodeAsync(codeHash, code);
                }
                await stateStore.SaveAccountAsync(addr, new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(balance),
                    CodeHash = codeHash
                });
                if (entry["storage"] is JObject storage)
                {
                    foreach (var slot in storage.Properties())
                    {
                        var slotKey = new BigInteger(slot.Name.HexToByteArray(), isUnsigned: true, isBigEndian: true);
                        var slotValue = slot.Value.ToString().HexToByteArray();
                        await stateStore.SaveStorageAsync(addr, slotKey, slotValue);
                    }
                }
            }
        }

        private static string LocateTestdataDir() => ProbedCandidates().FirstOrDefault(Directory.Exists);

        private static IEnumerable<string> ProbedCandidates()
        {
            var probe = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            while (probe != null)
            {
                yield return Path.GetFullPath(Path.Combine(probe, TestdataRelativePath));
                probe = Path.GetDirectoryName(probe);
            }
        }

        private static string TestdataNotFoundMessage()
        {
            var candidates = string.Join(Environment.NewLine, ProbedCandidates().Select(c => "  " + c));
            return
                $"Hive testdata not found. These are the live geth-parity scenarios and cannot be skipped.{Environment.NewLine}" +
                $"Looked for '{TestdataRelativePath}' walking up from the test binary at {AppContext.BaseDirectory}, " +
                $"trying:{Environment.NewLine}{candidates}{Environment.NewLine}" +
                "Expected layout — a go-ethereum checkout sitting beside this repository:" +
                $"{Environment.NewLine}  <repos>/Nethereum   (this repository){Environment.NewLine}" +
                $"  <repos>/go-ethereum/{TestdataRelativePath.Substring("go-ethereum/".Length)}{Environment.NewLine}" +
                "Run the suite from the repository root, or clone go-ethereum next to it.";
        }

        private static IReadOnlyList<BlockBundle> LoadChain()
        {
            var path = Path.Combine(TestdataDir, "chain.rlp");
            var bytes = File.ReadAllBytes(path);
            var keccak = new Sha3Keccack();
            var headerEncoder = new BlockHeaderEncoder();
            var list = new List<BlockBundle>();

            int pos = 0;
            while (pos < bytes.Length)
            {
                var blockColl = (RLPCollection)Nethereum.RLP.RLP.DecodeFirstElement(bytes, pos);
                int consumed = RlpStreamWalker.GetRlpItemLength(bytes, pos);

                var headerEncoded = RlpStreamWalker.ReEncodeAsList((RLPCollection)blockColl[0]);
                var header = headerEncoder.Decode(headerEncoded);
                var headerHash = keccak.CalculateHash(headerEncoded);

                var txs = new List<ISignedTransaction>();
                foreach (var txItem in (RLPCollection)blockColl[1])
                {
                    byte[] txBytes = txItem is RLPCollection c
                        ? RlpStreamWalker.ReEncodeAsList(c)
                        : txItem.RLPData;
                    txs.Add(TransactionFactory.CreateTransaction(txBytes));
                }

                var uncles = new List<BlockHeader>();
                foreach (var u in (RLPCollection)blockColl[2])
                    uncles.Add(headerEncoder.Decode(RlpStreamWalker.ReEncodeAsList((RLPCollection)u)));

                List<Withdrawal> withdrawals = null;
                if (blockColl.Count >= 4 && blockColl[3] is RLPCollection wList)
                {
                    withdrawals = new List<Withdrawal>();
                    foreach (var wItem in wList)
                    {
                        var wColl = (RLPCollection)wItem;
                        withdrawals.Add(new Withdrawal
                        {
                            Index = (ulong)wColl[0].RLPData.ToLongFromRLPDecoded(),
                            ValidatorIndex = (ulong)wColl[1].RLPData.ToLongFromRLPDecoded(),
                            Address = wColl[2].RLPData,
                            AmountInGwei = (ulong)wColl[3].RLPData.ToLongFromRLPDecoded()
                        });
                    }
                }

                list.Add(new BlockBundle(header, txs, uncles, withdrawals, headerHash));
                pos += consumed;
            }
            return list;
        }

        private static JObject LoadGenesis()
            => JObject.Parse(File.ReadAllText(Path.Combine(TestdataDir, "genesis.json")));

        private static IReadOnlyDictionary<string, JObject> LoadGenesisAllocRaw()
        {
            var alloc = (JObject)_genesis.Value["alloc"];
            var dict = new Dictionary<string, JObject>();
            foreach (var prop in alloc.Properties())
                dict[prop.Name] = (JObject)prop.Value;
            return dict;
        }

        private static class RlpStreamWalker
        {
            public static int GetRlpItemLength(byte[] data, int pos)
            {
                byte prefix = data[pos];
                if (prefix < 0x80) return 1;
                if (prefix < 0xb8) return 1 + (prefix - 0x80);
                if (prefix < 0xc0)
                {
                    int n = prefix - 0xb7;
                    int len = 0;
                    for (int i = 0; i < n; i++) len = (len << 8) | data[pos + 1 + i];
                    return 1 + n + len;
                }
                if (prefix < 0xf8) return 1 + (prefix - 0xc0);
                int nn = prefix - 0xf7;
                int llen = 0;
                for (int i = 0; i < nn; i++) llen = (llen << 8) | data[pos + 1 + i];
                return 1 + nn + llen;
            }

            public static byte[] ReEncodeAsList(RLPCollection coll)
            {
                var items = new byte[coll.Count][];
                for (int i = 0; i < coll.Count; i++)
                {
                    if (coll[i] is RLPCollection sub) items[i] = ReEncodeAsList(sub);
                    else items[i] = Nethereum.RLP.RLP.EncodeElement(coll[i].RLPData);
                }
                return Nethereum.RLP.RLP.EncodeList(items);
            }
        }
    }
}
