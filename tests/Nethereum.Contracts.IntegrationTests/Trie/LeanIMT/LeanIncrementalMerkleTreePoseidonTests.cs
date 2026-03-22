using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Merkle;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Trie.LeanIMT
{
    public class LeanIncrementalMerkleTreePoseidonTests
    {
        private readonly PoseidonPairHashProvider _hashProvider = new PoseidonPairHashProvider();
        private readonly ByteArrayToByteArrayConvertor _convertor = new ByteArrayToByteArrayConvertor();

        private LeanIncrementalMerkleTree<byte[]> CreateTree()
        {
            return new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor, PairingConcatType.Normal);
        }

        private static byte[] BigIntegerToBytes32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            if (bytes.Length > 32)
                Array.Copy(bytes, bytes.Length - 32, padded, 0, 32);
            else
                Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        private static BigInteger BytesToBigInteger(byte[] bytes)
        {
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void EmptyTree_HasEmptyRoot()
        {
            var tree = CreateTree();
            Assert.Empty(tree.Root);
            Assert.Equal(0, tree.Size);
            Assert.Equal(0, tree.Depth);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void SingleLeaf_RootEqualsHashOfLeaf()
        {
            var tree = CreateTree();
            var leaf = BigInteger.One;
            var leafBytes = BigIntegerToBytes32(leaf);
            tree.InsertLeaf(leafBytes);

            Assert.Equal(1, tree.Size);
            Assert.Equal(32, tree.Root.Length);

            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            var expectedRoot = hasher.HashBytesToBytes(leafBytes);
            Assert.Equal(expectedRoot, tree.Root);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void TwoLeaves_RootIsPoseidonOfBoth()
        {
            var tree = CreateTree();
            var leaf1 = BigIntegerToBytes32(BigInteger.One);
            var leaf2 = BigIntegerToBytes32(new BigInteger(2));
            tree.InsertLeaf(leaf1);
            tree.InsertLeaf(leaf2);

            Assert.Equal(2, tree.Size);
            Assert.Equal(1, tree.Depth);

            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            var hash1 = hasher.HashBytesToBytes(leaf1);
            var hash2 = hasher.HashBytesToBytes(leaf2);
            var expectedRoot = hasher.HashBytesToBytes(hash1, hash2);
            Assert.Equal(expectedRoot, tree.Root);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void FourLeaves_ProducesCorrectRoot()
        {
            var tree = CreateTree();
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);

            var leaves = new BigInteger[] { 1, 2, 3, 4 };
            var leafBytes = leaves.Select(l => BigIntegerToBytes32(l)).ToArray();

            foreach (var leaf in leafBytes)
                tree.InsertLeaf(leaf);

            Assert.Equal(4, tree.Size);
            Assert.Equal(2, tree.Depth);

            var h0 = hasher.HashBytesToBytes(leafBytes[0]);
            var h1 = hasher.HashBytesToBytes(leafBytes[1]);
            var h2 = hasher.HashBytesToBytes(leafBytes[2]);
            var h3 = hasher.HashBytesToBytes(leafBytes[3]);

            var h01 = hasher.HashBytesToBytes(h0, h1);
            var h23 = hasher.HashBytesToBytes(h2, h3);
            var expectedRoot = hasher.HashBytesToBytes(h01, h23);

            Assert.Equal(expectedRoot, tree.Root);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void InsertMany_MatchesSequentialInsert()
        {
            var tree1 = CreateTree();
            var tree2 = CreateTree();

            var leaves = Enumerable.Range(1, 16)
                .Select(i => BigIntegerToBytes32(new BigInteger(i)))
                .ToArray();

            foreach (var leaf in leaves)
                tree1.InsertLeaf(leaf);

            tree2.InsertMany(leaves);

            Assert.Equal(tree1.Root, tree2.Root);
            Assert.Equal(tree1.Size, tree2.Size);
            Assert.Equal(tree1.Depth, tree2.Depth);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void GenerateAndVerifyProof_AllLeaves()
        {
            var tree = CreateTree();
            var leaves = Enumerable.Range(1, 8)
                .Select(i => BigIntegerToBytes32(new BigInteger(i)))
                .ToArray();

            tree.InsertMany(leaves);

            for (int i = 0; i < leaves.Length; i++)
            {
                var proof = tree.GenerateProof(i);
                Assert.True(tree.VerifyProof(proof, leaves[i], tree.Root),
                    $"Proof verification failed for leaf at index {i}");
            }
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void GenerateProof_InvalidLeaf_VerificationFails()
        {
            var tree = CreateTree();
            var leaves = new BigInteger[] { 10, 20, 30, 40 };
            foreach (var leaf in leaves)
                tree.InsertLeaf(BigIntegerToBytes32(leaf));

            var proof = tree.GenerateProof(0);
            var wrongLeaf = BigIntegerToBytes32(new BigInteger(99));
            Assert.False(tree.VerifyProof(proof, wrongLeaf, tree.Root));
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void Has_ReturnsTrueForInsertedLeaf()
        {
            var tree = CreateTree();
            var leaf = BigIntegerToBytes32(BigInteger.One);
            tree.InsertLeaf(leaf);
            Assert.True(tree.Has(leaf));
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void Has_ReturnsFalseForMissingLeaf()
        {
            var tree = CreateTree();
            var leaf = BigIntegerToBytes32(BigInteger.One);
            tree.InsertLeaf(leaf);
            var missing = BigIntegerToBytes32(new BigInteger(2));
            Assert.False(tree.Has(missing));
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void IndexOf_ReturnsCorrectIndex()
        {
            var tree = CreateTree();
            var leaves = Enumerable.Range(1, 5)
                .Select(i => BigIntegerToBytes32(new BigInteger(i)))
                .ToArray();

            tree.InsertMany(leaves);

            for (int i = 0; i < leaves.Length; i++)
            {
                Assert.Equal(i, tree.IndexOf(leaves[i]));
            }
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void IndexOf_ReturnsMinusOneForMissing()
        {
            var tree = CreateTree();
            tree.InsertLeaf(BigIntegerToBytes32(BigInteger.One));
            Assert.Equal(-1, tree.IndexOf(BigIntegerToBytes32(new BigInteger(99))));
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void PoseidonPairHashProvider_SplitsTwoFieldElements()
        {
            var provider = new PoseidonPairHashProvider();
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);

            var left = BigIntegerToBytes32(new BigInteger(42));
            var right = BigIntegerToBytes32(new BigInteger(43));

            var combined = new byte[64];
            Array.Copy(left, 0, combined, 0, 32);
            Array.Copy(right, 0, combined, 32, 32);

            var hashFromProvider = provider.ComputeHash(combined);
            var hashFromHasher = hasher.HashBytesToBytes(left, right);

            Assert.Equal(hashFromHasher, hashFromProvider);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void PoseidonPairHashProvider_SingleElement_HashesAsOne()
        {
            var provider = new PoseidonPairHashProvider();
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);

            var input = BigIntegerToBytes32(new BigInteger(42));
            var hashFromProvider = provider.ComputeHash(input);
            var hashFromHasher = hasher.HashBytesToBytes(input);

            Assert.Equal(hashFromHasher, hashFromProvider);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void RootIsDeterministic_SameLeaves_SameRoot()
        {
            var tree1 = CreateTree();
            var tree2 = CreateTree();

            var leaves = new BigInteger[] { 100, 200, 300 };
            foreach (var leaf in leaves)
            {
                tree1.InsertLeaf(BigIntegerToBytes32(leaf));
                tree2.InsertLeaf(BigIntegerToBytes32(leaf));
            }

            Assert.Equal(tree1.Root, tree2.Root);
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void UpdateLeaf_ChangesRoot()
        {
            var tree = CreateTree();
            var leaves = new BigInteger[] { 1, 2, 3, 4 };
            foreach (var leaf in leaves)
                tree.InsertLeaf(BigIntegerToBytes32(leaf));

            var rootBefore = tree.Root.ToArray();
            tree.Update(0, BigIntegerToBytes32(new BigInteger(99)));
            Assert.False(rootBefore.SequenceEqual(tree.Root));
        }

        [Fact]
        [Trait("Category", "LeanIMT-Poseidon")]
        public void ProofNodes_ContainSiblingHashes()
        {
            var tree = CreateTree();
            var leaves = Enumerable.Range(1, 4)
                .Select(i => BigIntegerToBytes32(new BigInteger(i)))
                .ToArray();

            tree.InsertMany(leaves);

            var proof = tree.GenerateProof(0);
            Assert.Equal(2, proof.ProofNodes.Count);
            Assert.Equal(2, proof.PathIndices.Count);
            Assert.Equal(0, proof.PathIndices[0]);
            Assert.Equal(0, proof.PathIndices[1]);
        }
    }
}
