using Nethereum.Merkle;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Trie.LeanIMT
{
    public class LeanIncrementalMerkleTreeBigIntTests
    {
        private readonly IHashProvider _hashProvider = new SumHashProvider();
        private readonly IByteArrayConvertor<byte[]> _convertor = new IdentityConvertor();
        private const int TreeSize = 5;

        // Leaves 0..4 as byte arrays [0],[1],...
        private static byte[][] Leaves => Enumerable.Range(0, TreeSize)
                                                      .Select(i => new byte[] { (byte)i })
                                                      .ToArray();

        // Expected roots: after insert: ((0+1)+(2+3))+4 = 10; after update all to 0: 0
        private const byte ExpectedRootAfterInsert = 10;
        private const byte ExpectedRootAfterUpdate = 0;

        private class IdentityConvertor : IByteArrayConvertor<byte[]>
        {
            public byte[] ConvertToByteArray(byte[] input) => input;
            public byte[] ConvertFromByteArray(byte[] data) => data;
        }

        private class SumHashProvider : IHashProvider
        {
            // input is concatenated left||right; each is one byte
            public byte[] ComputeHash(byte[] input)
            {
                if (input.Length == 1)
                    return input;
                // sum two bytes
                var sum = (byte)(input[0] + input[1]);
                return new byte[] { sum };
            }
        }

        [Fact]
        public void NewTree_InitialState()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            Assert.Empty(tree.Root);
            Assert.Equal(0, tree.Depth);
            Assert.Equal("[[]]", tree.Export()); // JSON for empty: [[]]
            Assert.Empty(tree.Leaves);
            Assert.Equal(0, tree.Size);
        }

        [Fact]
        public void InsertLeaves_ShouldProduceExpectedRootAndDepth()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            foreach (var leaf in Leaves)
                tree.InsertLeaf(leaf);

            Assert.Equal(TreeSize, tree.Size);
            Assert.Equal((int)Math.Ceiling(Math.Log2(TreeSize)), tree.Depth);
            Assert.Single(tree.Root);
            Assert.Equal(ExpectedRootAfterInsert, tree.Root[0]);
        }

        [Fact]
        public void UpdateAllLeaves_ShouldProduceZeroRoot()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            tree.InsertMany(Leaves);
            // update all to zero
            for (int i = 0; i < TreeSize; i++)
                tree.Update(i, new byte[] { 0 });

            Assert.Equal(ExpectedRootAfterUpdate, tree.Root[0]);
        }

        [Fact]
        public void ExportImport_RoundTripPreservesTree()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            tree.InsertMany(Leaves);
            var json = tree.Export();
            var imported = LeanIncrementalMerkleTree<byte[]>.Import(
                _hashProvider,
                _convertor,
                json,
                str => Convert.FromBase64String(str)
            );
            Assert.Equal(tree.Size, imported.Size);
            Assert.Equal(tree.Depth, imported.Depth);
            Assert.Equal(tree.Root[0], imported.Root[0]);
        }

        [Fact]
        public void GenerateAndVerifyProof_ForEachLeaf_ShouldBeValid()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            tree.InsertMany(Leaves);
            for (int i = 0; i < TreeSize; i++)
            {
                var proof = tree.GenerateProof(i);
                Assert.True(tree.VerifyProof(proof, Leaves[i], tree.Root));
            }
        }

        [Fact]
        public void UpdateMany_ShouldMatchMultipleUpdates()
        {
            var tree1 = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            var tree2 = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            tree1.InsertMany(Leaves);
            tree2.InsertMany(Leaves);

            int[] indices = { 0, 2, 4 };
            var nodes = new[] { new byte[] { 10 }, new byte[] { 11 }, new byte[] { 12 } };
            // apply individual updates
            foreach (var (idx, node) in indices.Zip(nodes, Tuple.Create))
                tree1.Update(idx, node);
            // batch update
            tree2.UpdateMany(indices, nodes);

            Assert.Equal(tree1.Root[0], tree2.Root[0]);
        }


        [Fact]
        public void Import_WithMismatchedTreeData_ShouldThrow()
        {
            // Build small tree of 2 leaves
            var smallLeaves = new[] { new byte[] { 1 }, new byte[] { 2 } };
            var smallTree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            smallTree.InsertMany(smallLeaves);
            var validJson = smallTree.Export();
            // Tamper upper-level hash
            var nodes = JsonSerializer.Deserialize<List<List<string>>>(validJson);
            nodes[1][0] = Convert.ToBase64String(new byte[] { 255 });
            var tamperedJson = JsonSerializer.Serialize(nodes);
            Assert.Throws<ArgumentException>(() =>
                LeanIncrementalMerkleTree<byte[]>.Import(
                    _hashProvider,
                    _convertor,
                    tamperedJson,
                    str => Convert.FromBase64String(str)
                )
            );
        }

        [Fact]
        public void ExportAfterUpdate_Import_And_FurtherInserts_ShouldMatch()
        {
            var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
            tree.InsertMany(Leaves);
            tree.Update(0, new byte[] { 9 });
            var json = tree.Export();
            var imported = LeanIncrementalMerkleTree<byte[]>.Import(
                _hashProvider,
                _convertor,
                json,
                str => Convert.FromBase64String(str)
            );
            // Further insert
            var newLeaf = new byte[] { 5 };
            tree.InsertLeaf(newLeaf);
            imported.InsertLeaf(newLeaf);
            Assert.Equal(tree.Root[0], imported.Root[0]);
        }
    }
}

