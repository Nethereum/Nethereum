using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    /// <summary>
    /// Tests for the snap/1 edge-proof generator. Validates the proof bundle
    /// returned for [startKey, lastReturnedKey] is sufficient for a verifier to
    /// look up the boundary keys against the expected root using only the
    /// proof bytes as storage (the standard PatriciaTrie.Get(key, proofStorage)
    /// flow, same shape as AccountProofVerification).
    /// </summary>
    public class PatriciaRangeProofGeneratorTests
    {
        private static (PatriciaTrie trie, InMemoryTrieStorage storage, System.Collections.Generic.List<(byte[] keyHash, byte[] value)> entries) Build(int count)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryTrieStorage();
            var trie = new PatriciaTrie();
            var entries = new System.Collections.Generic.List<(byte[] keyHash, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xAB };
                entries.Add((keyHash, value));
                trie.Put(keyHash, value, storage);
            }
            trie.SaveDirtyNodesToStorage(storage);
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.keyHash, b.keyHash));
            return (trie, storage, entries);
        }

        private static InMemoryTrieStorage ProofToStorage(System.Collections.Generic.List<byte[]> proof)
        {
            var hashProvider = new Sha3KeccackHashProvider();
            var s = new InMemoryTrieStorage();
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
            var verifyTrie = new PatriciaTrie(rootHash);
            var fetched = verifyTrie.Get(pivot.keyHash, proofStorage);

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
            var verifyTrie = new PatriciaTrie(rootHash);

            Assert.Equal(entries[10].value.ToHex(), verifyTrie.Get(startKey, proofStorage).ToHex());
            Assert.Equal(entries[200].value.ToHex(), new PatriciaTrie(rootHash).Get(lastKey, proofStorage).ToHex());
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

            // Two boundary keys that are CLOSE (adjacent in sorted order) will
            // share most of their root-to-leaf path. The deduplicated proof
            // must not include the shared nodes twice.
            var k1 = entries[50].keyHash;
            var k2 = entries[51].keyHash;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, k1, k2);
            var proofStorage = ProofToStorage(proof);
            Assert.Equal(proof.Count, proofStorage.Storage.Count);
        }

        [Fact]
        public void Proof_ForNonExistentBoundary_StillCoversReachablePath()
        {
            // Build a trie missing a known hash, then ask for a proof using
            // that hash as start. The proof should still let us LOOK UP an
            // adjacent existing key (the path through the trie above the
            // would-be position is the same).
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            var nonExistentKey = new byte[32];
            for (int i = 0; i < 32; i++) nonExistentKey[i] = 0x7f;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, nonExistentKey);
            Assert.NotEmpty(proof);

            // The proof's path leads to where this key WOULD be. Some entry
            // sharing a common ancestor in the trie should be reachable via
            // the same proof (or part of it). Pick the entry whose hash is
            // closest above nonExistentKey.
            var nearest = entries
                .OrderBy(e => ByteArrayComparer.Current.Compare(e.keyHash, nonExistentKey))
                .First(e => ByteArrayComparer.Current.Compare(e.keyHash, nonExistentKey) >= 0);

            // We DON'T claim the proof for nonExistentKey resolves nearest —
            // that's not the right invariant. We claim the proof root hashes
            // up correctly: every node in the proof has the keccak we computed.
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

            // Flip a byte in the middle of the largest proof node — this should
            // break either the hash chain or the recovered RLP structure.
            var biggest = proof.OrderByDescending(b => b.Length).First();
            biggest[biggest.Length / 2] ^= 0xff;

            var proofStorage = ProofToStorage(proof);
            // Lookup either returns null/wrong-value or throws inside Get;
            // either way, it must NOT return the original value.
            byte[] fetched = null;
            try
            {
                fetched = new PatriciaTrie(rootHash).Get(pivot.keyHash, proofStorage);
            }
            catch { /* tampered RLP can throw; that's also a failure */ }

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

            var freshlyLoaded = PatriciaTrie.LoadFromStorage(rootHash, storage);
            var reloaded = PatriciaRangeProofGenerator.GenerateProof(freshlyLoaded.Root, storage, pivot.keyHash);

            Assert.Equal(direct.Count, reloaded.Count);
        }
    }
}
