using System.IO;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Builds the genesis <see cref="BlockHeader"/> for Geth's testdata chain
    /// from <c>genesis.json</c>: computes the state root over the alloc, then
    /// assembles a pre-London Frontier-style header (no baseFee, no withdrawals,
    /// no blob fields). Verifying that the computed hash matches the
    /// <c>chain.rlp</c> first-block parentHash is the canary.
    /// </summary>
    public static class GethTestdataGenesisBuilder
    {
        public static BlockHeader Build(string genesisJsonPath)
        {
            var genesis = JObject.Parse(File.ReadAllText(genesisJsonPath));

            // Build the alloc state and compute the genesis stateRoot.
            var stateStore = new InMemoryStateStore();
            var alloc = (JObject)genesis["alloc"];
            foreach (var prop in alloc.Properties())
            {
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;
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

            return new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = genesis["coinbase"]?.ToString() ?? "0x0000000000000000000000000000000000000000",
                StateRoot = stateRoot,
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = new HexBigInteger(genesis["difficulty"]?.ToString() ?? "0x0").Value.IsZero
                    ? EvmUInt256.Zero
                    : (EvmUInt256)new HexBigInteger(genesis["difficulty"].ToString()).Value,
                BlockNumber = EvmUInt256.Zero,
                GasLimit = (long)new HexBigInteger(genesis["gasLimit"].ToString()).Value,
                GasUsed = 0,
                Timestamp = (long)new HexBigInteger(genesis["timestamp"]?.ToString() ?? "0x0").Value,
                ExtraData = genesis["extraData"]?.ToString().HexToByteArray() ?? new byte[0],
                MixHash = genesis["mixHash"]?.ToString().HexToByteArray() ?? new byte[32],
                Nonce = genesis["nonce"] != null
                    ? PadLeft(new HexBigInteger(genesis["nonce"].ToString()).Value.ToByteArray(isUnsigned: true, isBigEndian: true), 8)
                    : new byte[8]
            };
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
