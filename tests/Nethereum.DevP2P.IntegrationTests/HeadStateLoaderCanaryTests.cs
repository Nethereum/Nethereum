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

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Canary test that proves our Patricia trie + Account encoding match
    /// Geth's byte-exact conventions: load Geth's testdata/headstate.json,
    /// rebuild the state trie via our APIs, assert the computed root equals
    /// the dump's claimed root. If this passes we know any snap response we
    /// generate is wire-compatible — which is the prerequisite for hitting
    /// `devp2p rlpx snap-test` for real.
    /// </summary>
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
            // Pick an account with many storage slots (the EIP-4788 beacon
            // root contract). If our storage encoding matches Geth's, this
            // single root will match — and the divergence isolates to the
            // state-trie level rather than storage.
            var path = FindHeadState();
            var doc = JObject.Parse(File.ReadAllText(path));
            var addr = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
            var entry = (JObject)doc["accounts"][addr];
            var expectedStorageRoot = ParseHex(entry["root"].ToString());

            var storage = new InMemoryTrieStorage();
            var trie = new PatriciaTrie();
            var keccak = new Sha3KeccackHashProvider();
            int slotCount = 0;
            foreach (var slot in ((JObject)entry["storage"]).Properties())
            {
                var rawKey = ParseHex(slot.Name);
                var trieKey = keccak.ComputeHash(rawKey);
                var v = ParseHex(slot.Value.ToString());
                var stripped = v.TrimZeroBytes();
                trie.Put(trieKey, RLP.RLP.EncodeElement(stripped), storage);
                slotCount++;
            }
            trie.SaveDirtyNodesToStorage(storage);
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
            // Search upward from the test bin dir for the sibling go-ethereum
            // clone's testdata directory.
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
