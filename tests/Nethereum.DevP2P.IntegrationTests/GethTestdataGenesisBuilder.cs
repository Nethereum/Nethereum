using System.IO;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.IntegrationTests
{
    public static class GethTestdataGenesisBuilder
    {
        public static BlockHeader Build(string genesisJsonPath)
        {
            var genesis = JObject.Parse(File.ReadAllText(genesisJsonPath));

            var stateStore = new InMemoryStateStore();
            var alloc = (JObject)genesis["alloc"];
            foreach (var prop in alloc.Properties())
            {
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;
                var balance = entry["balance"] != null
                    ? ParseHexOrDecimal(entry["balance"].ToString())
                    : BigInteger.Zero;
                ulong nonce = 0;
                if (entry["nonce"] != null)
                {
                    var nVal = ParseHexOrDecimal(entry["nonce"].ToString());
                    nonce = nVal.IsZero ? 0UL : (ulong)nVal;
                }
                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (entry["code"] != null)
                {
                    var code = entry["code"].ToString().HexToByteArray();
                    var keccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
                    codeHash = keccak.ComputeHash(code);
                    stateStore.SaveCodeAsync(codeHash, code).GetAwaiter().GetResult();
                }
                stateStore.SaveAccountAsync(addr, new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = codeHash
                }).GetAwaiter().GetResult();

                if (entry["storage"] is JObject storage)
                {
                    foreach (var slot in storage.Properties())
                    {
                        var slotKey = new BigInteger(slot.Name.HexToByteArray(), isUnsigned: true, isBigEndian: true);
                        var slotValue = slot.Value.ToString().HexToByteArray();
                        stateStore.SaveStorageAsync(addr, slotKey, slotValue).GetAwaiter().GetResult();
                    }
                }
            }
            var calculator = new IncrementalStateRootCalculator(stateStore);
            var stateRoot = calculator.ComputeStateRootAsync().GetAwaiter().GetResult();

            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = genesis["coinbase"]?.ToString() ?? "0x0000000000000000000000000000000000000000",
                StateRoot = stateRoot,
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = (EvmUInt256)ParseHexOrDecimal(genesis["difficulty"]?.ToString() ?? "0x0"),
                BlockNumber = EvmUInt256.Zero,
                GasLimit = (long)ParseHexOrDecimal(genesis["gasLimit"].ToString()),
                GasUsed = 0,
                Timestamp = (long)ParseHexOrDecimal(genesis["timestamp"]?.ToString() ?? "0x0"),
                ExtraData = genesis["extraData"]?.ToString().HexToByteArray() ?? new byte[0],
                MixHash = genesis["mixHash"]?.ToString().HexToByteArray() ?? new byte[32],
                Nonce = genesis["nonce"] != null
                    ? PadLeft(new HexBigInteger(genesis["nonce"].ToString()).Value.ToByteArray(isUnsigned: true, isBigEndian: true), 8)
                    : new byte[8]
            };
            if (genesis["config"] != null)
                ApplyForkFieldsAtGenesis(header, StandardGenesisLoader.Parse(genesis));
            return header;
        }

        private static void ApplyForkFieldsAtGenesis(BlockHeader header, StandardGenesisDocument document)
        {
            var fork = StandardGenesisLoader.BuildForkSchedule(document)
                .ResolveActivations()
                .ResolveAt(0, (ulong)header.Timestamp);
            if (fork >= HardforkName.London)
                header.BaseFee = (EvmUInt256)(document.BaseFeePerGas ?? BaseFeeCalculator.INITIAL_BASE_FEE);
            if (fork >= HardforkName.Cancun)
            {
                header.ExcessBlobGas = (long?)document.ExcessBlobGas;
                header.BlobGasUsed = (long?)document.BlobGasUsed;
            }
            GenesisHeaderFields.Apply(header, fork);
        }

        private static BigInteger ParseHexOrDecimal(string value)
        {
            return value.StartsWith("0x") || value.StartsWith("0X")
                ? new HexBigInteger(value).Value
                : BigInteger.Parse(value);
        }

        private static byte[] PadLeft(byte[] bytes, int length)
        {
            if (bytes.Length >= length) return bytes;
            var result = new byte[length];
            System.Buffer.BlockCopy(bytes, 0, result, length - bytes.Length, bytes.Length);
            return result;
        }
    }
}
