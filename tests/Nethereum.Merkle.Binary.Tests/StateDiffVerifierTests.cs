using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.StateDiff;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Merkle.Binary.Tests
{
    public class StateDiffVerifierTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider = new Blake3HashProvider();

        public StateDiffVerifierTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Verify_ValidDiff_Passes()
        {
            var (preTrie, diff) = ProduceOneDiff();
            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(diff, preTrie);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.StemsApplied > 0);
            Assert.True(result.SuffixesApplied > 0);
            _output.WriteLine($"Verified: {result.StemsApplied} stems, {result.SuffixesApplied} suffixes");
        }

        [Fact]
        public void Verify_TamperedPostRoot_Fails()
        {
            var (preTrie, diff) = ProduceOneDiff();
            diff.PostStateRoot[0] ^= 0xFF;

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(diff, preTrie);

            Assert.False(result.Success);
            Assert.Contains("Post-state root mismatch", result.ErrorMessage);
            _output.WriteLine($"Correctly rejected: {result.ErrorMessage}");
        }

        [Fact]
        public void Verify_TamperedPreRoot_Fails()
        {
            var (preTrie, diff) = ProduceOneDiff();
            diff.PreStateRoot[0] ^= 0xFF;

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(diff, preTrie);

            Assert.False(result.Success);
            Assert.Contains("Pre-state root mismatch", result.ErrorMessage);
        }

        [Fact]
        public void Verify_TamperedValue_Fails()
        {
            var (preTrie, diff) = ProduceOneDiff();
            diff.StemDiffs[0].SuffixDiffs[0].NewValue[0] ^= 0xFF;

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(diff, preTrie);

            Assert.False(result.Success);
            Assert.Contains("Post-state root mismatch", result.ErrorMessage);
        }

        [Fact]
        public void Verify_EmptyDiff_PassesWithSameRoot()
        {
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();
            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));

            var root = trie.ComputeRoot();
            var diff = new BinaryTrieStateDiff
            {
                BlockNumber = 1,
                PreStateRoot = root,
                PostStateRoot = root
            };

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(diff, trie);

            Assert.True(result.Success);
            Assert.Equal(0, result.StemsApplied);
        }

        [Fact]
        public void VerifySequence_ThreeBlocks_Passes()
        {
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var store = new InMemoryBinaryTrieNodeStore();
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));
            var startRoot = trie.ComputeRoot();
            var startTrie = trie.Copy();

            var diffs = new List<BinaryTrieStateDiff>();

            for (int block = 1; block <= 3; block++)
            {
                var preRoot = trie.ComputeRoot();
                trie.SaveToStorage(store);
                store.ClearDirtyTracking();
                store.MarkBlockCommitted(block);

                trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, (EvmUInt256)block),
                    PadTo32(new byte[] { (byte)(block * 10) }));
                var postRoot = trie.ComputeRoot();
                trie.SaveToStorage(store);

                diffs.Add(BinaryTrieStateDiffProducer.Produce(block, preRoot, postRoot, store));
            }

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.VerifySequence(diffs.ToArray(), startTrie);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.StemsApplied >= 3);
            _output.WriteLine($"Verified 3-block sequence: {result.StemsApplied} total stems, {result.SuffixesApplied} total suffixes");
        }

        [Fact]
        public void VerifySequence_TamperedMiddleBlock_Fails()
        {
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var store = new InMemoryBinaryTrieNodeStore();
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));
            var startTrie = trie.Copy();

            var diffs = new List<BinaryTrieStateDiff>();

            for (int block = 1; block <= 3; block++)
            {
                var preRoot = trie.ComputeRoot();
                trie.SaveToStorage(store);
                store.ClearDirtyTracking();
                store.MarkBlockCommitted(block);

                trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, (EvmUInt256)block),
                    PadTo32(new byte[] { (byte)(block * 10) }));
                var postRoot = trie.ComputeRoot();
                trie.SaveToStorage(store);

                diffs.Add(BinaryTrieStateDiffProducer.Produce(block, preRoot, postRoot, store));
            }

            diffs[1].PostStateRoot[0] ^= 0xFF;

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.VerifySequence(diffs.ToArray(), startTrie);

            Assert.False(result.Success);
            Assert.Contains("diff 1", result.ErrorMessage);
            _output.WriteLine($"Correctly rejected tampered block 2: {result.ErrorMessage}");
        }

        [Fact]
        public void Verify_RoundTripWithEncoder_Passes()
        {
            var (preTrie, diff) = ProduceOneDiff();

            var encoded = BinaryTrieStateDiffEncoder.Encode(diff);
            var decoded = BinaryTrieStateDiffEncoder.Decode(encoded);

            var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
            var result = verifier.Verify(decoded, preTrie);

            Assert.True(result.Success, result.ErrorMessage);
            _output.WriteLine($"Encode → decode → verify: {encoded.Length} bytes, {result.StemsApplied} stems");
        }

        [Fact]
        public void Verify_Poseidon_Passes()
        {
            var poseidon = new PoseidonPairHashProvider();
            var trie = new BinaryTrie(poseidon);
            var keyDeriv = new BinaryTreeKeyDerivation(poseidon);
            var store = new InMemoryBinaryTrieNodeStore();
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));
            var preRoot = trie.ComputeRoot();
            var preTrie = trie.Copy();

            trie.SaveToStorage(store);
            store.ClearDirtyTracking();
            store.MarkBlockCommitted(1);

            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, EvmUInt256.Zero),
                PadTo32(new byte[] { 0x42 }));
            var postRoot = trie.ComputeRoot();
            trie.SaveToStorage(store);

            var diff = BinaryTrieStateDiffProducer.Produce(1, preRoot, postRoot, store);

            var verifier = new BinaryTrieStateDiffVerifier(poseidon);
            var result = verifier.Verify(diff, preTrie);

            Assert.True(result.Success, result.ErrorMessage);
            _output.WriteLine($"Poseidon verified: {result.StemsApplied} stems");
        }

        private (BinaryTrie preTrie, BinaryTrieStateDiff diff) ProduceOneDiff()
        {
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var store = new InMemoryBinaryTrieNodeStore();
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));
            var preRoot = trie.ComputeRoot();
            var preTrie = trie.Copy();

            trie.SaveToStorage(store);
            store.ClearDirtyTracking();
            store.MarkBlockCommitted(1);

            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, EvmUInt256.Zero),
                PadTo32(new byte[] { 0x42 }));
            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, (EvmUInt256)1),
                PadTo32(new byte[] { 0x43 }));
            var postRoot = trie.ComputeRoot();
            trie.SaveToStorage(store);

            var diff = BinaryTrieStateDiffProducer.Produce(1, preRoot, postRoot, store);
            return (preTrie, diff);
        }

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length >= 32) return value;
            var padded = new byte[32];
            System.Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
