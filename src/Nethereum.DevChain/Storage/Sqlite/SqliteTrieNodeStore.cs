using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteTrieNodeStore : ITrieNodeStore
    {
        private readonly SqliteStorageManager _manager;
        private readonly ConcurrentDictionary<byte[], byte[]> _cache;
        private readonly ConcurrentDictionary<byte[], byte> _dirty;
        private long _savepointCounter;

        public SqliteTrieNodeStore(SqliteStorageManager manager)
        {
            _manager = manager;
            _cache = new ConcurrentDictionary<byte[], byte[]>(new ByteArrayComparer());
            _dirty = new ConcurrentDictionary<byte[], byte>(new ByteArrayComparer());
        }

        public void Put(byte[] key, byte[] value)
        {
            if (key == null) return;
            _cache[key] = value;
            _dirty.TryAdd(key, 0);
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT node_data FROM trie_nodes WHERE node_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", key);

            var data = cmd.ExecuteScalar() as byte[];
            if (data != null)
            {
                _cache.TryAdd(key, data);
            }
            return data;
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;
            _cache.TryRemove(key, out _);
            _dirty.TryRemove(key, out _);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM trie_nodes WHERE node_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", key);
            cmd.ExecuteNonQuery();
        }

        public bool ContainsKey(byte[] key)
        {
            if (key == null) return false;

            if (_cache.ContainsKey(key))
                return true;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM trie_nodes WHERE node_hash = @hash LIMIT 1";
            cmd.Parameters.AddWithValue("@hash", key);

            return cmd.ExecuteScalar() != null;
        }

        public void Flush()
        {
            if (_dirty.IsEmpty) return;

            var dirtyKeys = new List<byte[]>(_dirty.Keys);
            _dirty.Clear();

            var sp = $"tn_{Interlocked.Increment(ref _savepointCounter)}";
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                foreach (var key in dirtyKeys)
                {
                    if (_cache.TryGetValue(key, out var value))
                    {
                        using var cmd = _manager.Connection.CreateCommand();
                        cmd.CommandText = "INSERT OR REPLACE INTO trie_nodes (node_hash, node_data) VALUES (@hash, @data)";
                        cmd.Parameters.AddWithValue("@hash", key);
                        cmd.Parameters.AddWithValue("@data", (object)value ?? System.DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }
        }

        private void ExecuteSql(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public void Clear()
        {
            _cache.Clear();
            _dirty.Clear();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM trie_nodes";
            cmd.ExecuteNonQuery();
        }
    }
}
