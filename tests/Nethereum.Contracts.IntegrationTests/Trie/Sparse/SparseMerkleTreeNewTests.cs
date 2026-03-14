using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Merkle.Sparse;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class SparseMerkleTreeNewTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;

        public SparseMerkleTreeNewTests(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
        }

        [Fact]
        public async Task NewImplementation_SimpleTest_ShouldWork()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);
            
            // Act
            var emptyRoot = await tree.GetRootHashAsync();
            _output.WriteLine($"Empty root: {emptyRoot}");
            
            await tree.SetLeafAsync("10", "test_value");
            var newRoot = await tree.GetRootHashAsync();
            _output.WriteLine($"New root: {newRoot}");
            
            // Assert
            Assert.NotEqual(emptyRoot, newRoot);
            
            var leafCount = await tree.GetLeafCountAsync();
            Assert.Equal(1, leafCount);
        }

        [Fact]
        public async Task NewImplementation_MultipleLeaves_ShouldWork()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);
            
            // Act
            await tree.SetLeafAsync("10", "value1");
            await tree.SetLeafAsync("20", "value2");
            await tree.SetLeafAsync("30", "value3");
            
            var rootHash = await tree.GetRootHashAsync();
            var leafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Root with 3 leaves: {rootHash}");
            _output.WriteLine($"Leaf count: {leafCount}");
            
            // Assert
            Assert.Equal(3, leafCount);
            Assert.NotEqual(tree.EmptyLeafHash, rootHash);
        }

        [Fact]
        public async Task NewImplementation_RemoveLeaf_ShouldWork()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);
            
            // Act
            await tree.SetLeafAsync("10", "value1");
            var rootWithLeaf = await tree.GetRootHashAsync();
            
            await tree.SetLeafAsync("10", null); // Remove leaf
            var rootAfterRemoval = await tree.GetRootHashAsync();
            
            var leafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Root with leaf: {rootWithLeaf}");
            _output.WriteLine($"Root after removal: {rootAfterRemoval}");
            _output.WriteLine($"Leaf count: {leafCount}");
            
            // Assert
            Assert.Equal(0, leafCount);
            Assert.NotEqual(rootWithLeaf, rootAfterRemoval);
        }

        [Fact]
        public async Task Determinism_SameLeavesDifferentOrder_ShouldProduceSameRoot()
        {
            var storage1 = new InMemorySparseMerkleTreeStorage<string>();
            var storage2 = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree1 = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage1);
            var tree2 = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage2);

            await tree1.SetLeafAsync("0010", "alpha");
            await tree1.SetLeafAsync("0020", "beta");
            await tree1.SetLeafAsync("0030", "gamma");

            await tree2.SetLeafAsync("0030", "gamma");
            await tree2.SetLeafAsync("0010", "alpha");
            await tree2.SetLeafAsync("0020", "beta");

            var root1 = await tree1.GetRootHashAsync();
            var root2 = await tree2.GetRootHashAsync();

            _output.WriteLine($"Root (order 1): {root1}");
            _output.WriteLine($"Root (order 2): {root2}");
            Assert.Equal(root1, root2);
        }

        [Fact]
        public async Task InsertRemoveRoundtrip_ShouldReturnToEmptyRoot()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);

            var emptyRoot = await tree.GetRootHashAsync();

            await tree.SetLeafAsync("00ab", "value1");
            await tree.SetLeafAsync("00cd", "value2");
            var nonEmptyRoot = await tree.GetRootHashAsync();
            Assert.NotEqual(emptyRoot, nonEmptyRoot);

            await tree.SetLeafAsync("00ab", null);
            await tree.SetLeafAsync("00cd", null);
            var rootAfterRemoval = await tree.GetRootHashAsync();

            _output.WriteLine($"Empty root:          {emptyRoot}");
            _output.WriteLine($"Root after removal:  {rootAfterRemoval}");
            Assert.Equal(emptyRoot, rootAfterRemoval);
        }

        [Fact]
        public async Task BatchVsSequential_ShouldProduceSameRoot()
        {
            var storage1 = new InMemorySparseMerkleTreeStorage<string>();
            var storage2 = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var treeSeq = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage1);
            var treeBatch = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage2);

            var data = new Dictionary<string, string>
            {
                ["0001"] = "one",
                ["0002"] = "two",
                ["0003"] = "three",
                ["00ff"] = "max"
            };

            foreach (var kvp in data)
                await treeSeq.SetLeafAsync(kvp.Key, kvp.Value);

            await treeBatch.SetLeavesAsync(data);

            var seqRoot = await treeSeq.GetRootHashAsync();
            var batchRoot = await treeBatch.GetRootHashAsync();

            _output.WriteLine($"Sequential root: {seqRoot}");
            _output.WriteLine($"Batch root:      {batchRoot}");
            Assert.Equal(seqRoot, batchRoot);
        }

        [Fact]
        public async Task UpdateLeaf_ShouldChangeRoot()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);

            await tree.SetLeafAsync("0a", "original");
            var root1 = await tree.GetRootHashAsync();

            await tree.SetLeafAsync("0a", "updated");
            var root2 = await tree.GetRootHashAsync();

            _output.WriteLine($"Root before update: {root1}");
            _output.WriteLine($"Root after update:  {root2}");
            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public async Task MultipleInsertRemoveCycles_ShouldMaintainCorrectState()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);

            var emptyRoot = await tree.GetRootHashAsync();

            await tree.SetLeafAsync("0001", "a");
            await tree.SetLeafAsync("0002", "b");
            Assert.Equal(2, await tree.GetLeafCountAsync());

            await tree.SetLeafAsync("0001", null);
            Assert.Equal(1, await tree.GetLeafCountAsync());

            await tree.SetLeafAsync("0003", "c");
            await tree.SetLeafAsync("0004", "d");
            Assert.Equal(3, await tree.GetLeafCountAsync());

            var rootWith3 = await tree.GetRootHashAsync();

            await tree.SetLeafAsync("0002", null);
            await tree.SetLeafAsync("0003", null);
            await tree.SetLeafAsync("0004", null);
            Assert.Equal(0, await tree.GetLeafCountAsync());

            var finalRoot = await tree.GetRootHashAsync();
            _output.WriteLine($"Empty root: {emptyRoot}");
            _output.WriteLine($"Final root: {finalRoot}");
            Assert.Equal(emptyRoot, finalRoot);
        }
    }
}