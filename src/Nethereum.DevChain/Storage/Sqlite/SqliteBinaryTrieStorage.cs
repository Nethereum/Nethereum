using Microsoft.Data.Sqlite;
using Nethereum.Merkle.Binary.Storage;

namespace Nethereum.DevChain.Storage.Sqlite
{
    /// <summary>
    /// SQLite-backed <see cref="IBinaryTrieStorage"/> for EIP-7864 binary-trie
    /// nodes. Stores in the <c>binary_trie_nodes</c> table, separate from the
    /// Patricia <c>trie_nodes</c> table so the two schemas can evolve
    /// independently (different node shapes, different hash).
    /// </summary>
    public class SqliteBinaryTrieStorage : IBinaryTrieStorage
    {
        private readonly SqliteStorageManager _manager;

        public SqliteBinaryTrieStorage(SqliteStorageManager manager)
        {
            _manager = manager;
        }

        public void Put(byte[] key, byte[] value)
        {
            if (key == null) return;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO binary_trie_nodes (node_hash, node_data) VALUES (@hash, @data)";
            cmd.Parameters.AddWithValue("@hash", key);
            cmd.Parameters.AddWithValue("@data", (object)value ?? System.DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT node_data FROM binary_trie_nodes WHERE node_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", key);

            return cmd.ExecuteScalar() as byte[];
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM binary_trie_nodes WHERE node_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", key);
            cmd.ExecuteNonQuery();
        }
    }
}
