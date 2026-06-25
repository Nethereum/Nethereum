using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Loads the canonical Ethereum mainnet (chainId 1) genesis allocation
    /// from the embedded <c>Resources/MainnetGenesis.json</c> resource — the
    /// exact 8,893-entry canonical mainnet genesis allocation. Returns a
    /// populated
    /// <see cref="InMemoryStateStore"/> and an empty
    /// <see cref="InMemoryTrieNodeStore"/> ready to seed
    /// <see cref="Nethereum.CoreChain.BlockProcessor"/>.
    /// <para>
    /// The mainnet alloc emits balances as plain decimal strings
    /// (e.g. <c>"200000000000000000000"</c>) — not hex-prefixed like the
    /// Hive testdata flavour. <see cref="ParseBalance"/> handles both forms.
    /// </para>
    /// </summary>
    public static class MainnetGenesisLoader
    {
        /// <summary>
        /// Populate <paramref name="stateStore"/> with the canonical mainnet
        /// genesis allocation. Returns the number of accounts loaded. Both
        /// in-memory and RocksDB-backed IStateStore implementations work.
        /// </summary>
        public static async Task<int> PopulateAsync(IStateStore stateStore)
        {
            using var stream = OpenEmbeddedGenesis();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return await BuildAsync(JObject.Parse(json), stateStore);
        }

        private static Stream OpenEmbeddedGenesis()
        {
            var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "Nethereum.DevP2P.SyncNode.Resources.MainnetGenesis.json";
            var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException(
                    $"Embedded resource '{resourceName}' not found. Available: " +
                    string.Join(", ", asm.GetManifestResourceNames()));
            return stream;
        }

        private static async Task<int> BuildAsync(JObject genesis, IStateStore stateStore)
        {
            var alloc = (JObject)genesis["alloc"];
            int n = 0;

            foreach (var prop in alloc.Properties())
            {
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;

                var balance = ParseBalance(entry["balance"]?.ToString());
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
                    await stateStore.SaveCodeAsync(codeHash, code);
                }

                await stateStore.SaveAccountAsync(addr, new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
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

                n++;
            }

            return n;
        }

        private static BigInteger ParseBalance(string value)
        {
            if (string.IsNullOrEmpty(value)) return BigInteger.Zero;
            return (value.StartsWith("0x") || value.StartsWith("0X"))
                ? new HexBigInteger(value).Value
                : BigInteger.Parse(value);
        }
    }
}
