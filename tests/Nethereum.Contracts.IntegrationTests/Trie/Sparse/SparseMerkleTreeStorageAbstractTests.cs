using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Sparse.Storage;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    /// <summary>
    /// Comprehensive tests for the abstract storage implementation
    /// Uses mock repository to verify proper delegation and operation tracking
    /// </summary>
    public class SparseMerkleTreeStorageAbstractTests
    {
        private readonly ITestOutputHelper _output;
        private readonly MockSparseMerkleRepository<string> _mockRepository;
        private readonly DatabaseSparseMerkleTreeStorage<string> _storage;

        public SparseMerkleTreeStorageAbstractTests(ITestOutputHelper output)
        {
            _output = output;
            _mockRepository = new MockSparseMerkleRepository<string>();
            _storage = new DatabaseSparseMerkleTreeStorage<string>(_mockRepository);
        }

        [Fact]
        public async Task SetLeafAsync_WithValidValue_ShouldCallRepositorySetLeaf()
        {
            // Arrange
            var key = "testkey";
            var value = "testvalue";

            // Act
            await _storage.SetLeafAsync(key, value);

            // Assert
            Assert.Equal(1, _mockRepository.SetLeafCallCount);
            Assert.Contains($"SetLeaf({key}, {value})", _mockRepository.OperationLog);
            Assert.Equal(value, _mockRepository.Leaves[key]);
        }

        [Fact]
        public async Task SetLeafAsync_WithNullValue_ShouldCallRepositoryRemoveLeaf()
        {
            // Arrange
            var key = "testkey";
            await _storage.SetLeafAsync(key, "initialvalue");
            _mockRepository.ClearOperationLog();

            // Act
            await _storage.SetLeafAsync(key, null);

            // Assert
            Assert.Equal(1, _mockRepository.RemoveLeafCallCount); // Should call RemoveLeaf for null values
            Assert.Contains($"RemoveLeaf({key})", _mockRepository.OperationLog);
            Assert.False(_mockRepository.Leaves.ContainsKey(key));
        }

        [Fact]
        public async Task GetLeafAsync_ShouldCallRepositoryGetLeaf()
        {
            // Arrange
            var key = "testkey";
            var value = "testvalue";
            await _mockRepository.SetLeafAsync(key, value);
            _mockRepository.ClearOperationLog();

            // Act
            var result = await _storage.GetLeafAsync(key);

            // Assert
            Assert.Equal(1, _mockRepository.GetLeafCallCount);
            Assert.Contains($"GetLeaf({key})", _mockRepository.OperationLog);
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task RemoveLeafAsync_ShouldCallRepositoryRemoveLeaf()
        {
            // Arrange
            var key = "testkey";

            // Act
            await _storage.RemoveLeafAsync(key);

            // Assert
            Assert.Equal(1, _mockRepository.RemoveLeafCallCount);
            Assert.Contains($"RemoveLeaf({key})", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task HasLeavesInSubtreeAsync_LeafLevel_ShouldCallRepositoryLeafExists()
        {
            // Arrange
            var nodeKey = "testkey";
            var level = 0; // Leaf level
            var treeDepth = 8;

            // Act
            await _storage.HasLeavesInSubtreeAsync(nodeKey, level, treeDepth);

            // Assert
            Assert.Equal(1, _mockRepository.LeafExistsCallCount);
            Assert.Contains($"LeafExists({nodeKey})", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task HasLeavesInSubtreeAsync_RootLevel_ShouldCallRepositoryGetLeafCount()
        {
            // Arrange
            var nodeKey = "00000000";
            var level = 8; // Root level
            var treeDepth = 8;

            // Act
            await _storage.HasLeavesInSubtreeAsync(nodeKey, level, treeDepth);

            // Assert
            Assert.Equal(1, _mockRepository.GetLeafCountCallCount);
            Assert.Contains("GetLeafCount()", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task HasLeavesInSubtreeAsync_IntermediateLevel_ShouldCallRepositoryGetLeafKeys()
        {
            // Arrange
            var nodeKey = "0000";
            var level = 4; // Intermediate level
            var treeDepth = 8;

            // Act
            await _storage.HasLeavesInSubtreeAsync(nodeKey, level, treeDepth);

            // Assert
            Assert.Equal(1, _mockRepository.GetLeafKeysCallCount);
            Assert.Contains("GetLeafKeys()", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task SetCachedNodeAsync_ShouldCallRepositorySetCachedNode()
        {
            // Arrange
            var key = "cachekey";
            var hash = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            await _storage.SetCachedNodeAsync(key, hash);

            // Assert
            Assert.Equal(1, _mockRepository.SetCachedNodeCallCount);
            Assert.Contains($"SetCachedNode({key}, {hash.Length} bytes)", _mockRepository.OperationLog);
            Assert.Equal(hash, _mockRepository.Cache[key]);
        }

        [Fact]
        public async Task GetCachedNodeAsync_ShouldCallRepositoryGetCachedNode()
        {
            // Arrange
            var key = "cachekey";
            var hash = new byte[] { 0x01, 0x02, 0x03 };
            await _mockRepository.SetCachedNodeAsync(key, hash);
            _mockRepository.ClearOperationLog();

            // Act
            var result = await _storage.GetCachedNodeAsync(key);

            // Assert
            Assert.Equal(1, _mockRepository.GetCachedNodeCallCount);
            Assert.Contains($"GetCachedNode({key})", _mockRepository.OperationLog);
            Assert.Equal(hash, result);
        }

        [Fact]
        public async Task RemoveCachedNodeAsync_ShouldCallRepositoryRemoveCachedNode()
        {
            // Arrange
            var key = "cachekey";

            // Act
            await _storage.RemoveCachedNodeAsync(key);

            // Assert
            Assert.Equal(1, _mockRepository.RemoveCachedNodeCallCount);
            Assert.Contains($"RemoveCachedNode({key})", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task ClearAsync_ShouldCallRepositoryClearAllLeavesAndCache()
        {
            // Act
            await _storage.ClearAsync();

            // Assert
            Assert.Equal(1, _mockRepository.ClearAllLeavesCallCount);
            Assert.Equal(1, _mockRepository.ClearAllCacheCallCount);
            Assert.Contains("ClearAllLeaves()", _mockRepository.OperationLog);
            Assert.Contains("ClearAllCache()", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task ClearCacheAsync_ShouldCallRepositoryClearAllCache()
        {
            // Act
            await _storage.ClearCacheAsync();

            // Assert
            Assert.Equal(1, _mockRepository.ClearAllCacheCallCount);
            Assert.Contains("ClearAllCache()", _mockRepository.OperationLog);
        }

        [Fact]
        public async Task GetLeafCountAsync_ShouldCallRepositoryGetLeafCount()
        {
            // Act
            var result = await _storage.GetLeafCountAsync();

            // Assert
            Assert.Equal(1, _mockRepository.GetLeafCountCallCount);
            Assert.Contains("GetLeafCount()", _mockRepository.OperationLog);
            Assert.Equal(0, result); // Empty repository should return 0
        }

        [Fact]
        public async Task IsOptimizedForLargeDatasets_ShouldCallRepositoryIsOptimizedForLargeDatasets()
        {
            // Act
            var result = await _storage.IsOptimizedForLargeDatasets();

            // Assert
            Assert.Equal(1, _mockRepository.IsOptimizedForLargeDatasetsCallCount);
            Assert.Contains("IsOptimizedForLargeDatasets()", _mockRepository.OperationLog);
            Assert.False(result); // Mock repository returns false
        }

        [Fact]
        public async Task HasLeavesInSubtreeAsync_WithMatchingLeafInSubtree_ShouldReturnTrue()
        {
            // Arrange
            var leafKey = "10000000"; // Clear first bit = 1, rest = 0
            var nodeKey = "10000000"; // Same exact prefix
            var level = 4; // Check at 4-bit level
            var treeDepth = 32;

            // Add a leaf that should be in the subtree
            await _mockRepository.SetLeafAsync(leafKey, "testvalue");
            _mockRepository.ClearOperationLog();

            // Act
            var result = await _storage.HasLeavesInSubtreeAsync(nodeKey, level, treeDepth);

            // Assert
            Assert.True(result);
            Assert.Equal(1, _mockRepository.GetLeafKeysCallCount);
        }

        [Fact]
        public async Task HasLeavesInSubtreeAsync_WithNonMatchingLeafInSubtree_ShouldReturnFalse()
        {
            // Arrange
            var leafKey = "87654321"; // Different prefix
            var nodeKey = "12300000"; // Different prefix
            var level = 4; // Check at 4-bit level
            var treeDepth = 32;

            // Add a leaf that should NOT be in the subtree
            await _mockRepository.SetLeafAsync(leafKey, "testvalue");
            _mockRepository.ClearOperationLog();

            // Act
            var result = await _storage.HasLeavesInSubtreeAsync(nodeKey, level, treeDepth);

            // Assert
            Assert.False(result);
            Assert.Equal(1, _mockRepository.GetLeafKeysCallCount);
        }

        [Fact]
        public async Task MultipleOperations_ShouldTrackAllOperationsCorrectly()
        {
            // Arrange & Act
            await _storage.SetLeafAsync("key1", "value1");
            await _storage.SetLeafAsync("key2", "value2");
            await _storage.GetLeafAsync("key1");
            await _storage.SetCachedNodeAsync("cache1", new byte[] { 0x01 });
            await _storage.GetCachedNodeAsync("cache1");
            await _storage.RemoveLeafAsync("key2");
            await _storage.ClearCacheAsync();

            // Assert
            Assert.Equal(2, _mockRepository.SetLeafCallCount);
            Assert.Equal(1, _mockRepository.GetLeafCallCount);
            Assert.Equal(1, _mockRepository.SetCachedNodeCallCount);
            Assert.Equal(1, _mockRepository.GetCachedNodeCallCount);
            Assert.Equal(1, _mockRepository.RemoveLeafCallCount);
            Assert.Equal(1, _mockRepository.ClearAllCacheCallCount);

            // Verify operation log contains all operations
            Assert.Contains("SetLeaf(key1, value1)", _mockRepository.OperationLog);
            Assert.Contains("SetLeaf(key2, value2)", _mockRepository.OperationLog);
            Assert.Contains("GetLeaf(key1)", _mockRepository.OperationLog);
            Assert.Contains("SetCachedNode(cache1, 1 bytes)", _mockRepository.OperationLog);
            Assert.Contains("GetCachedNode(cache1)", _mockRepository.OperationLog);
            Assert.Contains("RemoveLeaf(key2)", _mockRepository.OperationLog);
            Assert.Contains("ClearAllCache()", _mockRepository.OperationLog);
        }
    }
}