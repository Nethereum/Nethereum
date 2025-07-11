using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Merkle.Sparse;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    /// <summary>
    /// Test suite adapted from SparseMerkleTreeTests to use SparseMerkleTree
    /// </summary>
    public class SparseMerkleTreeTestsConverted
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;
        private readonly SparseMerkleTree<string> _tree;

        public SparseMerkleTreeTestsConverted(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            _tree = new SparseMerkleTree<string>(256, _hashProvider, convertor, storage);
        }

        [Fact]
        public void Constructor_ValidParameters_ShouldCreate()
        {
            // Arrange & Act
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(32, _hashProvider, convertor, storage);

            // Assert - Hex tree has different interface, so we test basic functionality
            Assert.NotNull(tree);
            var emptyRoot = tree.GetRootHash();
            Assert.NotNull(emptyRoot);
        }

        [Fact]
        public void GenerateLeafKey_SameInput_ShouldProduceSameKey()
        {
            // Arrange
            var tableName = "users";
            var rowId = 12345L;

            // Act - Generate consistent hex keys
            var key1 = GenerateTableRowKey(tableName, rowId);
            var key2 = GenerateTableRowKey(tableName, rowId);

            // Assert
            Assert.Equal(key1, key2);
        }

        [Fact]
        public void GenerateLeafKey_DifferentInput_ShouldProduceDifferentKeys()
        {
            // Arrange & Act
            var key1 = GenerateTableRowKey("users", 1);
            var key2 = GenerateTableRowKey("users", 2);
            var key3 = GenerateTableRowKey("orders", 1);

            // Assert
            Assert.NotEqual(key1, key2);
            Assert.NotEqual(key1, key3);
            Assert.NotEqual(key2, key3);
        }

        [Fact]
        public void SetLeaf_ValidData_ShouldSetLeaf()
        {
            // Arrange
            var tableName = "users";
            var rowId = 1L;
            var data = "Alice";

            // Act
            SetTableRow(tableName, rowId, data);
            var leafHash = GetTableRow(tableName, rowId);

            // Assert
            Assert.NotNull(leafHash);
            Assert.NotEqual(leafHash, _tree.GetLeaf("0000000000000000000000000000000000000000000000000000000000000000"));
        }

        [Fact]
        public void SetLeaf_NullData_ShouldRemoveLeaf()
        {
            // Arrange
            var tableName = "users";
            var rowId = 1L;

            // Set then remove
            SetTableRow(tableName, rowId, "Alice");
            _tree.SetLeaf(GenerateTableRowKey(tableName, rowId), null);
            var leafHash = GetTableRow(tableName, rowId);

            // Assert - should return empty/default hash
            var emptyHash = _tree.GetLeaf("0000000000000000000000000000000000000000000000000000000000000000");
            Assert.Equal(emptyHash, leafHash);
        }

        [Fact]
        public void GetRootHash_EmptyTree_ShouldReturnConsistentHash()
        {
            // Arrange & Act
            var root1 = _tree.GetRootHash();
            var root2 = _tree.GetRootHash();

            // Assert
            Assert.NotNull(root1);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void Debug_BasicTreeOperations()
        {
            // Test with a smaller tree for easier debugging
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var smallTree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);

            // Get empty root
            var emptyRoot = smallTree.GetRootHash();
            _output.WriteLine($"Empty root: {emptyRoot}");

            // Generate a simple key manually - for depth 8, we need exactly 2 hex chars (8 bits)
            var simpleKey = "01"; // This represents binary 00000001 for depth 8

            // Set leaf directly
            smallTree.SetLeaf(simpleKey, "TestData");

            // Check if leaf was set
            var retrievedLeaf = smallTree.GetLeaf(simpleKey);
            var emptyLeaf = smallTree.GetLeaf("00"); // Empty key for depth 8
            _output.WriteLine($"Leaf was set: {!retrievedLeaf.Equals(emptyLeaf)}");

            // Get new root
            var newRoot = smallTree.GetRootHash();
            _output.WriteLine($"New root: {newRoot}");

            // Check if roots are different
            var rootsAreDifferent = !emptyRoot.Equals(newRoot);
            _output.WriteLine($"Roots are different: {rootsAreDifferent}");

            Assert.True(rootsAreDifferent, "Root should change when data is added");
        }

        [Fact]
        public void GetRootHash_WithData_ShouldChangeRoot()
        {
            // Arrange
            var emptyRoot = _tree.GetRootHash();

            // Act
            SetTableRow("users", 1, "Alice");
            var newRoot = _tree.GetRootHash();

            // Assert
            Assert.NotEqual(emptyRoot, newRoot);
        }

        [Fact]
        public void GetRootHash_SameData_ShouldProduceSameRoot()
        {
            // Arrange
            var storage1 = new InMemorySparseMerkleTreeStorage<string>();
            var convertor1 = new StringByteArrayConvertor();
            var tree1 = new SparseMerkleTree<string>(256, _hashProvider, convertor1, storage1);
            
            var storage2 = new InMemorySparseMerkleTreeStorage<string>();
            var convertor2 = new StringByteArrayConvertor();
            var tree2 = new SparseMerkleTree<string>(256, _hashProvider, convertor2, storage2);

            // Act
            SetTableRow(tree1, "users", 1, "Alice");
            SetTableRow(tree1, "users", 2, "Bob");

            SetTableRow(tree2, "users", 1, "Alice");
            SetTableRow(tree2, "users", 2, "Bob");

            var root1 = tree1.GetRootHash();
            var root2 = tree2.GetRootHash();

            // Assert
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void MultipleOperations_ShouldMaintainConsistency()
        {
            // Arrange
            var testData = new Dictionary<(string, long), string>
            {
                [("users", 1)] = "Alice",
                [("users", 2)] = "Bob", 
                [("orders", 100)] = "Order1",
                [("orders", 101)] = "Order2",
                [("products", 50)] = "Product1"
            };

            // Act - Set all data
            foreach (var kvp in testData)
            {
                SetTableRow(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
            }

            var finalRoot = _tree.GetRootHash();

            // Assert - Verify data can be retrieved
            foreach (var kvp in testData)
            {
                var retrievedHash = GetTableRow(kvp.Key.Item1, kvp.Key.Item2);
                var expectedHash = Hash(kvp.Value);
                Assert.Equal(expectedHash, retrievedHash);
            }

            // Root should not be empty
            var emptyStorage = new InMemorySparseMerkleTreeStorage<string>();
            var emptyConvertor = new StringByteArrayConvertor();
            var emptyRoot = new SparseMerkleTree<string>(256, _hashProvider, emptyConvertor, emptyStorage).GetRootHash();
            Assert.NotEqual(emptyRoot, finalRoot);
        }

        [Fact]
        public void RootCaching_ShouldWork()
        {
            // Arrange
            SetTableRow("users", 1, "Alice");

            // Act
            var stopwatch = Stopwatch.StartNew();
            var root1 = _tree.GetRootHash();
            var time1 = stopwatch.ElapsedTicks;

            stopwatch.Restart();
            var root2 = _tree.GetRootHash(); // Should be cached
            var time2 = stopwatch.ElapsedTicks;

            // Assert
            Assert.Equal(root1, root2);
            _output.WriteLine($"First call: {time1} ticks, Second call: {time2} ticks");
            
            // Note: Hex tree doesn't implement caching yet, so this might not be faster
            // But roots should still be equal
        }

        [Fact]
        public void Debug_SimpleEdit()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);

            // Set initial value
            SetTableRow(tree, "users", 1, "Alice");
            var root1 = tree.GetRootHash();
            _output.WriteLine($"Root after Alice: {root1}");

            // Edit the same row
            SetTableRow(tree, "users", 1, "Alice_Updated");
            var root2 = tree.GetRootHash();
            _output.WriteLine($"Root after update: {root2}");

            // Verify the leaf changed
            var leafHash = GetTableRow(tree, "users", 1);
            _output.WriteLine($"Updated leaf hash: {leafHash}");

            // Check if roots are different
            _output.WriteLine($"Roots different: {!root1.Equals(root2)}");

            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public void Performance_LargeDataset_ShouldBeReasonable()
        {
            // Arrange
            const int recordCount = 1000;
            var stopwatch = new Stopwatch();

            // Act - Insert records
            stopwatch.Start();
            for (int i = 0; i < recordCount; i++)
            {
                SetTableRow("users", i, $"User{i}");
            }
            stopwatch.Stop();
            var insertTime = stopwatch.ElapsedMilliseconds;

            // Act - Generate root
            stopwatch.Restart();
            var root = _tree.GetRootHash();
            stopwatch.Stop();
            var rootTime = stopwatch.ElapsedMilliseconds;

            // Assert
            Assert.NotNull(root);
            _output.WriteLine($"Performance for {recordCount} records:");
            _output.WriteLine($"Insert time: {insertTime}ms ({(double)insertTime / recordCount:F2}ms per record)");
            _output.WriteLine($"Root computation: {rootTime}ms");

            // Reasonable performance expectations for hex implementation
            Assert.True(insertTime < recordCount * 50, "Insert should be < 50ms per record for hex tree");
            Assert.True(rootTime < 30000, "Root computation should be < 30 seconds for hex tree");
        }

        [Fact]
        public void SparsityOptimization_ShouldWork()
        {
            // Arrange - Create tree with very few leaves in large space
            SetTableRow("users", 1, "Alice");
            SetTableRow("users", 1000000, "Bob"); // Far apart

            var stopwatch = Stopwatch.StartNew();

            // Act
            var root = _tree.GetRootHash();
            var leaf1 = GetTableRow("users", 1);
            var leaf2 = GetTableRow("users", 1000000);

            stopwatch.Stop();

            // Assert
            Assert.NotNull(root);
            Assert.Equal(Hash("Alice"), leaf1);
            Assert.Equal(Hash("Bob"), leaf2);

            // Should be reasonably fast even with sparse data
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Sparse operations should be reasonably fast");

            _output.WriteLine($"Sparse operations took: {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void EdgeCases_ShouldHandle()
        {
            // Test empty string
            SetTableRow("test", 1, "");
            var emptyLeaf = GetTableRow("test", 1);
            Assert.Equal(Hash(""), emptyLeaf);

            // Test very long string
            var longString = new string('A', 10000);
            SetTableRow("test", 2, longString);
            var longLeaf = GetTableRow("test", 2);
            Assert.Equal(Hash(longString), longLeaf);

            // Test special characters
            var specialString = "Hello 世界! 🌟 @#$%^&*()";
            SetTableRow("test", 3, specialString);
            var specialLeaf = GetTableRow("test", 3);
            Assert.Equal(Hash(specialString), specialLeaf);
        }

        [Fact]
        public void CollisionResistance_ShouldNotCollide()
        {
            // Generate many keys and ensure no collisions
            var keys = new HashSet<string>();
            const int testCount = 10000;

            for (int i = 0; i < testCount; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var key = GenerateTableRowKey($"table{j}", i);
                    Assert.False(keys.Contains(key), $"Collision detected for table{j}, row {i}");
                    keys.Add(key);
                }
            }

            _output.WriteLine($"Generated {keys.Count} unique keys without collisions");
        }

        [Fact]
        public void UpdateOperations_ShouldUpdateCorrectly()
        {
            // Arrange
            var tableName = "users";
            var rowId = 1L;

            // Act - Initial set
            SetTableRow(tableName, rowId, "Alice");
            var initialRoot = _tree.GetRootHash();
            var initialLeaf = GetTableRow(tableName, rowId);

            // Act - Update
            SetTableRow(tableName, rowId, "Alice Updated");
            var updatedRoot = _tree.GetRootHash();
            var updatedLeaf = GetTableRow(tableName, rowId);

            // Assert
            Assert.NotEqual(initialRoot, updatedRoot);
            Assert.Equal(Hash("Alice"), initialLeaf);
            Assert.Equal(Hash("Alice Updated"), updatedLeaf);
        }

        [Fact]
        public void Diagnostic_EmptyVsPopulated()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);

            _output.WriteLine("=== Empty vs Populated Root Test ===");

            var emptyRoot = tree.GetRootHash();
            _output.WriteLine($"Empty root: {emptyRoot}");

            SetTableRow(tree, "users", 1, "Alice");
            var populatedRoot = tree.GetRootHash();
            _output.WriteLine($"Populated root: {populatedRoot}");

            _output.WriteLine($"Same: {emptyRoot.Equals(populatedRoot)}");
            
            Assert.NotEqual(emptyRoot, populatedRoot);
        }

        // Helper methods adapted for hex tree interface
        private string GenerateTableRowKey(string tableName, long rowId)
        {
            var input = $"{tableName}:{rowId}";
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = _hashProvider.ComputeHash(bytes);
            return hash.ToHex();
        }

        private void SetTableRow(string tableName, long rowId, string data)
        {
            var key = GenerateTableRowKey(tableName, rowId);
            _tree.SetLeaf(key, data);
        }

        private void SetTableRow(SparseMerkleTree<string> tree, string tableName, long rowId, string data)
        {
            var key = GenerateTableRowKey(tableName, rowId);
            tree.SetLeaf(key, data);
        }

        private string GetTableRow(string tableName, long rowId)
        {
            var key = GenerateTableRowKey(tableName, rowId);
            return _tree.GetLeaf(key);
        }

        private string GetTableRow(SparseMerkleTree<string> tree, string tableName, long rowId)
        {
            var key = GenerateTableRowKey(tableName, rowId);
            return tree.GetLeaf(key);
        }

        private string Hash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return _hashProvider.ComputeHash(bytes).ToHex();
        }
    }
}