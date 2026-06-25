using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Test-suite fixture for go-ethereum's
    /// <c>cmd/devp2p/internal/ethtest/testdata</c> Hive chain. Locates the
    /// sibling go-ethereum checkout, decodes <c>chain.rlp</c> into
    /// <see cref="BlockBundle"/>s once per test run, and exposes the genesis
    /// allocation + per-chain factories needed to build a follower stack.
    /// When <see cref="IsAvailable"/> is false the chain.rlp could not be
    /// located — callers should skip the scenario.
    /// </summary>
    public static class HiveTestdataFixture
    {
        public const string TestdataRelativePath =
            "go-ethereum/cmd/devp2p/internal/ethtest/testdata";

        private static readonly Lazy<string> _testdataDir = new(LocateTestdataDir);
        private static readonly Lazy<IReadOnlyList<BlockBundle>> _chain = new(LoadChain);
        private static readonly Lazy<IReadOnlyDictionary<string, JObject>> _genesisAllocRaw =
            new(LoadGenesisAllocRaw);

        public static bool IsAvailable => _testdataDir.Value != null;

        public static string TestdataDir =>
            _testdataDir.Value ?? throw new DirectoryNotFoundException(
                $"Hive testdata not found at sibling <repo-parent>/{TestdataRelativePath}.");

        public static IReadOnlyList<BlockBundle> Chain => _chain.Value;

        public static IReadOnlyDictionary<string, JObject> GenesisAllocRaw => _genesisAllocRaw.Value;

        public static IChainActivations ChainActivations { get; } = new HiveActivations();

        public static Func<HardforkName, HardforkConfig> HardforkConfigFactory { get; } =
            f => DefaultMainnetHardforkRegistry.Instance.Get(f);

        public static Func<HardforkName, ChainConfig> ChainConfigFactory { get; } =
            f => new ChainConfig
            {
                ChainId = BigInteger.Parse("3503995874084926"),
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

        private static string LocateTestdataDir()
        {
            var probe = AppContext.BaseDirectory;
            while (probe != null)
            {
                var candidate = Path.GetFullPath(Path.Combine(probe, TestdataRelativePath));
                if (Directory.Exists(candidate)) return candidate;
                probe = Path.GetDirectoryName(probe);
            }
            return null;
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

        private static IReadOnlyDictionary<string, JObject> LoadGenesisAllocRaw()
        {
            var path = Path.Combine(TestdataDir, "genesis.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var alloc = (JObject)json["alloc"];
            var dict = new Dictionary<string, JObject>();
            foreach (var prop in alloc.Properties())
                dict[prop.Name] = (JObject)prop.Value;
            return dict;
        }

        private sealed class HiveActivations : IChainActivations
        {
            public const long HomesteadBlock         = 0;
            public const long TangerineWhistleBlock  = 6;
            public const long SpuriousDragonBlock    = 12;
            public const long ByzantiumBlock         = 18;
            public const long ConstantinopleBlock    = 24;
            public const long PetersburgBlock        = 30;
            public const long IstanbulBlock          = 36;
            public const long MuirGlacierBlock       = 42;
            public const long BerlinBlock            = 48;
            public const long LondonBlock            = 54;
            public const long ArrowGlacierBlock      = 60;
            public const long GrayGlacierBlock       = 66;
            public const long ParisBlock             = 72;
            public const ulong ShanghaiTimestamp = 780;
            public const ulong CancunTimestamp   = 840;

            public HardforkName ResolveAt(long blockNumber, ulong timestamp)
            {
                if (timestamp >= CancunTimestamp)   return HardforkName.Cancun;
                if (timestamp >= ShanghaiTimestamp) return HardforkName.Shanghai;
                if (blockNumber >= ParisBlock)            return HardforkName.Paris;
                if (blockNumber >= GrayGlacierBlock)      return HardforkName.GrayGlacier;
                if (blockNumber >= ArrowGlacierBlock)     return HardforkName.ArrowGlacier;
                if (blockNumber >= LondonBlock)           return HardforkName.London;
                if (blockNumber >= BerlinBlock)           return HardforkName.Berlin;
                if (blockNumber >= MuirGlacierBlock)      return HardforkName.MuirGlacier;
                if (blockNumber >= IstanbulBlock)         return HardforkName.Istanbul;
                if (blockNumber >= PetersburgBlock)       return HardforkName.Petersburg;
                if (blockNumber >= ConstantinopleBlock)   return HardforkName.Constantinople;
                if (blockNumber >= ByzantiumBlock)        return HardforkName.Byzantium;
                if (blockNumber >= SpuriousDragonBlock)   return HardforkName.SpuriousDragon;
                if (blockNumber >= TangerineWhistleBlock) return HardforkName.TangerineWhistle;
                if (blockNumber >= HomesteadBlock)        return HardforkName.Homestead;
                return HardforkName.Frontier;
            }
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
