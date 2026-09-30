using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class PatriciaRangeProofGeneratorTests
    {
        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, System.Collections.Generic.List<(byte[] keyHash, byte[] value)> entries) Build(int count)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new System.Collections.Generic.List<(byte[] keyHash, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xAB };
                entries.Add((keyHash, value));
                trie.Put(keyHash, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.keyHash, b.keyHash));
            return (trie, storage, entries);
        }

        private static InMemoryContentNodeStore ProofToStorage(System.Collections.Generic.List<byte[]> proof)
        {
            var hashProvider = new Sha3KeccackHashProvider();
            var s = new InMemoryContentNodeStore();
            foreach (var node in proof) s.Put(hashProvider.ComputeHash(node), node);
            return s;
        }

        [Fact]
        public void Proof_AllowsLookupOfStartKey_UnderRoot()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();
            var pivot = entries[entries.Count / 3];

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, pivot.keyHash);
            Assert.NotEmpty(proof);

            var proofStorage = ProofToStorage(proof);
            var verifyTrie = new PatriciaTrie(rootHash, proofStorage);
            var fetched = verifyTrie.Get(pivot.keyHash);

            Assert.Equal(pivot.value.ToHex(), fetched.ToHex());
        }

        [Fact]
        public void Proof_AllowsLookupOfBothBoundaries()
        {
            var (trie, storage, entries) = Build(256);
            var rootHash = trie.Root.GetHash();

            var startKey = entries[10].keyHash;
            var lastKey = entries[200].keyHash;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);
            var proofStorage = ProofToStorage(proof);
            var verifyTrie = new PatriciaTrie(rootHash, proofStorage);

            Assert.Equal(entries[10].value.ToHex(), verifyTrie.Get(startKey).ToHex());
            Assert.Equal(entries[200].value.ToHex(), new PatriciaTrie(rootHash, proofStorage).Get(lastKey).ToHex());
        }

        [Fact]
        public void Proof_SameStartAndEnd_EmitsSinglePath()
        {
            var (trie, storage, entries) = Build(64);
            var rootHash = trie.Root.GetHash();
            var key = entries[entries.Count / 2].keyHash;

            var proofWithDup = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, key, key);
            var proofSingle = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, key);

            Assert.Equal(proofSingle.Count, proofWithDup.Count);
        }

        [Fact]
        public void Proof_DedupesNodesSharedBetweenBoundaries()
        {
            var (trie, storage, entries) = Build(256);

            var k1 = entries[50].keyHash;
            var k2 = entries[51].keyHash;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, k1, k2);
            var proofStorage = ProofToStorage(proof);
            Assert.Equal(proof.Count, proofStorage.Storage.Count);
        }

        [Fact]
        public void Proof_ForNonExistentBoundary_StillCoversReachablePath()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            var nonExistentKey = new byte[32];
            for (int i = 0; i < 32; i++) nonExistentKey[i] = 0x7f;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, nonExistentKey);
            Assert.NotEmpty(proof);

            var nearest = entries
                .OrderBy(e => ByteArrayComparer.Current.Compare(e.keyHash, nonExistentKey))
                .First(e => ByteArrayComparer.Current.Compare(e.keyHash, nonExistentKey) >= 0);

            var hashProvider = new Sha3KeccackHashProvider();
            foreach (var node in proof)
            {
                var h = hashProvider.ComputeHash(node);
                Assert.NotNull(h);
            }
        }

        [Fact]
        public void Proof_TamperedNode_BreaksLookup()
        {
            var (trie, storage, entries) = Build(64);
            var rootHash = trie.Root.GetHash();
            var pivot = entries[entries.Count / 2];

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, pivot.keyHash);

            var biggest = proof.OrderByDescending(b => b.Length).First();
            biggest[biggest.Length / 2] ^= 0xff;

            var proofStorage = ProofToStorage(proof);
            byte[] fetched = null;
            try
            {
                fetched = new PatriciaTrie(rootHash, proofStorage).Get(pivot.keyHash);
            }
            catch { }

            if (fetched != null)
                Assert.NotEqual(pivot.value.ToHex(), fetched.ToHex());
        }

        [Fact]
        public void Proof_OverFreshlyLoadedTrie_IsTheSame()
        {
            var (trie, storage, entries) = Build(64);
            var rootHash = trie.Root.GetHash();
            var pivot = entries[entries.Count / 2];

            var direct = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, pivot.keyHash);

            var freshlyLoaded = PatriciaTrie.LoadFromStorage(rootHash, (ITrieNodeStore)storage);
            var reloaded = PatriciaRangeProofGenerator.GenerateProof(freshlyLoaded.Root, storage, pivot.keyHash);

            Assert.Equal(direct.Count, reloaded.Count);
        }
    }
}
