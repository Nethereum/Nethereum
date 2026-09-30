using System.IO;
using System.Linq;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.RLP;
using Nethereum.Util.HashProviders;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class HeadStateLoaderCanaryTests
    {
        private readonly ITestOutputHelper _output;
        public HeadStateLoaderCanaryTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void Load_GethTestdataHeadState_ProducesMatchingStateRoot()
        {
            var path = FindHeadState();
            _output.WriteLine($"Loading: {path}");

            var result = HeadStateLoader.Load(path);
            _output.WriteLine($"Accounts: {result.AccountCount}");
            _output.WriteLine($"Bytecodes: {result.Bytecodes.Count}");
            _output.WriteLine($"Expected root: {result.ExpectedRoot.ToHex()}");
            _output.WriteLine($"Computed root: {result.ComputedRoot.ToHex()}");

            Assert.True(result.RootMatches,
                $"State root mismatch — our Patricia/Account encoding diverges from Geth.\n"
                + $"  Expected: {result.ExpectedRoot.ToHex()}\n"
                + $"  Computed: {result.ComputedRoot.ToHex()}");
        }

        [Fact]
        public void Load_SingleAccountStorageTrie_RootMatches()
        {
            var path = FindHeadState();
            var doc = JObject.Parse(File.ReadAllText(path));
            var addr = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
            var entry = (JObject)doc["accounts"][addr];
            var expectedStorageRoot = ParseHex(entry["root"].ToString());

            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var keccak = new Sha3KeccackHashProvider();
            int slotCount = 0;
            foreach (var slot in ((JObject)entry["storage"]).Properties())
            {
                var rawKey = ParseHex(slot.Name);
                var trieKey = keccak.ComputeHash(rawKey);
                var v = ParseHex(slot.Value.ToString());
                var stripped = v.TrimZeroBytes();
                trie.Put(trieKey, RLP.RLP.EncodeElement(stripped));
                slotCount++;
            }
            trie.SaveDirtyNodesToStorage();
            var computed = trie.Root.GetHash();

            _output.WriteLine($"Slots: {slotCount}");
            _output.WriteLine($"Expected storage root: {expectedStorageRoot.ToHex()}");
            _output.WriteLine($"Computed storage root: {computed.ToHex()}");
            Assert.Equal(expectedStorageRoot.ToHex(), computed.ToHex());
        }

        private static byte[] ParseHex(string s)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            if (s.Length == 0) return new byte[0];
            if (s.Length % 2 != 0) s = "0" + s;
            return s.HexToByteArray();
        }
        private static string FindHeadState()
        {
            var probe = System.AppContext.BaseDirectory;
            while (probe != null)
            {
                var sibling = Path.Combine(probe, "..", "go-ethereum", "cmd", "devp2p", "internal", "ethtest", "testdata", "headstate.json");
                if (File.Exists(sibling)) return Path.GetFullPath(sibling);
                probe = Path.GetDirectoryName(probe);
            }
            throw new FileNotFoundException(
                "headstate.json not found. Expected at ../go-ethereum/cmd/devp2p/internal/ethtest/testdata/ relative to repo root.");
        }
    }
}
