using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.Merkle.Patricia.Tests
{
    using ProofVerification = Nethereum.Merkle.Patricia.ProofVerification.ProofVerification;

    public class PatriciaRangeProofVerifierTests
    {
        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] keyHash, byte[] value)> entries) Build(int count, int seed = 0)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] keyHash, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD };
                entries.Add((keyHash, value));
                trie.Put(keyHash, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.keyHash, b.keyHash));
            return (trie, storage, entries);
        }

        [Fact]
        public void Verify_FullTrie_NoEdgeProof_Succeeds()
        {
            var (trie, _, entries) = Build(40);
            var rootHash = trie.Root.GetHash();

            var keys = entries.Select(e => e.keyHash).ToList();
            var values = entries.Select(e => e.value).ToList();

            var r = ProofVerification.Current.Range.Verify(rootHash, keys[0], keys, values, proofNodes: null);
            Assert.True(r.Valid);
            Assert.False(r.HasMore);
        }

        [Fact]
        public void Verify_FullTrie_NoEdgeProof_WrongRoot_Fails()
        {
            var (_, _, entries) = Build(40);
            var wrongRoot = new byte[32];
            for (int i = 0; i < 32; i++) wrongRoot[i] = 0xAA;

            var keys = entries.Select(e => e.keyHash).ToList();
            var values = entries.Select(e => e.value).ToList();
            var r = ProofVerification.Current.Range.Verify(wrongRoot, keys[0], keys, values, proofNodes: null);
            Assert.False(r.Valid);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-range-proofs", "Generate and verify a snap/1 bounded range proof and read HasMore", Order = 1)]
        [Fact]
        public void Verify_BoundedRange_RoundTrip_Succeeds_WithHasMore()
        {
            var (trie, storage, entries) = Build(256);
            var rootHash = trie.Root.GetHash();

            int startIdx = 30, endIdx = 80;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.value).ToList();

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.True(r.Valid);
            Assert.True(r.HasMore);
        }

        [Fact]
        public void Verify_BoundedRange_LastChunk_HasNoMore()
        {
            var (trie, storage, entries) = Build(64);
            var rootHash = trie.Root.GetHash();

            int startIdx = 50;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[entries.Count - 1].keyHash;
            var keys = entries.Skip(startIdx).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Select(e => e.value).ToList();

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.True(r.Valid);
            Assert.False(r.HasMore);
        }

        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] keyHash, byte[] value)> entries) BuildSharedPrefixTrieWithExtendedNodeOnPath(int count, byte prefixByte = 0x7A)
        {
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] keyHash, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = new byte[32];
                for (int b = 0; b < 30; b++) keyHash[b] = prefixByte;
                var keySpacingLeavingGapForCursor = i * 4;
                keyHash[30] = (byte)(keySpacingLeavingGapForCursor >> 8);
                keyHash[31] = (byte)(keySpacingLeavingGapForCursor & 0xff);
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD };
                entries.Add((keyHash, value));
                trie.Put(keyHash, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.keyHash, b.keyHash));
            return (trie, storage, entries);
        }

        [Fact]
        public void Verify_TailPage_SharedPrefixOnPath_Succeeds()
        {
            var (trie, storage, entries) = BuildSharedPrefixTrieWithExtendedNodeOnPath(64);
            var rootHash = trie.Root.GetHash();

            var cursor = (byte[])entries[62].keyHash.Clone();
            cursor[31] += 2;
            var lastKey = entries[63].keyHash;
            var keys = new List<byte[]> { entries[63].keyHash };
            var values = new List<byte[]> { entries[63].value };

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, cursor, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, cursor, keys, values, proof);
            Assert.True(r.Valid);
            Assert.False(r.HasMore);
        }

        [Fact]
        public void Verify_CappedMidPage_SharedPrefixOnPath_Succeeds_WithHasMore()
        {
            var (trie, storage, entries) = BuildSharedPrefixTrieWithExtendedNodeOnPath(64);
            var rootHash = trie.Root.GetHash();

            int startIdx = 20, endIdx = 25;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.value).ToList();

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.True(r.Valid);
            Assert.True(r.HasMore);
        }

        [Fact]
        public void Verify_TamperedValue_Fails()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            int startIdx = 10, endIdx = 50;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => (byte[])e.value.Clone()).ToList();

            values[5][0] ^= 0xff;

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        public void Verify_DroppedKey_Fails()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            int startIdx = 10, endIdx = 50;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;

            var range = entries.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
            range.RemoveAt(range.Count / 2);
            var keys = range.Select(e => e.keyHash).ToList();
            var values = range.Select(e => e.value).ToList();

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        public void Verify_OutOfOrderKeys_Fails()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            int startIdx = 10, endIdx = 50;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.value).ToList();

            (keys[3], keys[4]) = (keys[4], keys[3]);
            (values[3], values[4]) = (values[4], values[3]);

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        public void Verify_SingleElement_Succeeds()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            var pivot = entries[64];
            var keys = new List<byte[]> { pivot.keyHash };
            var values = new List<byte[]> { pivot.value };
            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, pivot.keyHash);

            var r = ProofVerification.Current.Range.Verify(rootHash, pivot.keyHash, keys, values, proof);
            Assert.True(r.Valid);
            Assert.True(r.HasMore);
        }

        [Fact]
        public void Verify_SingleElement_WrongValue_Fails()
        {
            var (trie, storage, entries) = Build(64);
            var rootHash = trie.Root.GetHash();

            var pivot = entries[32];
            var keys = new List<byte[]> { pivot.keyHash };
            var values = new List<byte[]> { new byte[] { 0xde, 0xad, 0xbe, 0xef } };
            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, pivot.keyHash);

            var r = ProofVerification.Current.Range.Verify(rootHash, pivot.keyHash, keys, values, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        public void Verify_FirstChunk_FromZeroStart_HasMore()
        {
            var (trie, storage, entries) = Build(256);
            var rootHash = trie.Root.GetHash();

            var startKey = new byte[32];
            int endIdx = 50;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Take(endIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Take(endIdx + 1).Select(e => e.value).ToList();

            var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.True(r.Valid);
            Assert.True(r.HasMore);
        }

        [Fact]
        public void Verify_AcceptsRangeFromFreshlyLoadedTrie()
        {
            var (trie, storage, entries) = Build(128);
            var rootHash = trie.Root.GetHash();

            int startIdx = 30, endIdx = 60;
            var startKey = entries[startIdx].keyHash;
            var lastKey = entries[endIdx].keyHash;
            var keys = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.keyHash).ToList();
            var values = entries.Skip(startIdx).Take(endIdx - startIdx + 1).Select(e => e.value).ToList();

            var loaded = PatriciaTrie.LoadFromStorage(rootHash, (ITrieNodeStore)storage);
            var proof = PatriciaRangeProofGenerator.GenerateProof(loaded.Root, storage, startKey, lastKey);

            var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
            Assert.True(r.Valid);
        }
    }
}
