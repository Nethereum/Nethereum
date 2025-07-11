using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Merkle;
using Nethereum.Merkle.Sparse;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public static class SparseMerkleTreeExtensions
    {
        /// <summary>
        /// Generate a deterministic key from table name and row ID
        /// </summary>
        public static string GenerateTableRowKey<T>(this SparseMerkleTree<T> tree, string tableName, long rowId)
        {
            var keyString = $"{tableName}_{rowId}";
            var hash = tree.HashProvider.ComputeHash(System.Text.Encoding.UTF8.GetBytes(keyString));
            return hash.ToHex();
        }

        /// <summary>
        /// Set a leaf using table name and row ID
        /// </summary>
        public static async Task SetTableRowAsync<T>(this SparseMerkleTree<T> tree, string tableName, long rowId, T item)
        {
            var key = tree.GenerateTableRowKey(tableName, rowId);
            await tree.SetLeafAsync(key, item);
        }

        /// <summary>
        /// Get a leaf using table name and row ID
        /// </summary>
        public static async Task<T> GetTableRowAsync<T>(this SparseMerkleTree<T> tree, string tableName, long rowId)
        {
            var key = tree.GenerateTableRowKey(tableName, rowId);
            return await tree.GetLeafAsync(key);
        }

        // Note: Proof generation methods will be added when proof functionality is implemented

        /// <summary>
        /// Batch set multiple table rows efficiently
        /// </summary>
        public static async Task SetTableRowsAsync<T>(this SparseMerkleTree<T> tree, string tableName, Dictionary<long, T> rows)
        {
            var tasks = rows.Select(kvp => tree.SetTableRowAsync(tableName, kvp.Key, kvp.Value));
            await Task.WhenAll(tasks);
        }
    }
}