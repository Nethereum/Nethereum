using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteBinaryTrieNodeStore : IBinaryTrieNodeStore
    {
        private readonly SqliteStorageManager _manager;
        private readonly ConcurrentDictionary<byte[], byte> _dirtyHashes =
            new ConcurrentDictionary<byte[], byte>(new ByteArrayComparer());
        private long _currentBlock;

        public SqliteBinaryTrieNodeStore(SqliteStorageManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public int NodeCount
        {
            get
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM binary_trie_nodes";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void Put(byte[] key, byte[] value)
        {
            PutNode(key, value, -1, 0, null);
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

            using var cmd2 = _manager.Connection.CreateCommand();
            cmd2.CommandText = "DELETE FROM binary_trie_addr_stems WHERE node_hash = @hash";
            cmd2.Parameters.AddWithValue("@hash", key);
            cmd2.ExecuteNonQuery();

            _dirtyHashes.TryRemove(key, out _);
        }

        public void PutNode(byte[] hash, byte[] encoded, int depth, byte nodeType, byte[] stem)
        {
            if (hash == null) return;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO binary_trie_nodes
                (node_hash, node_data, depth, node_type, block_number, stem)
                VALUES (@hash, @data, @depth, @type, @block, @stem)";
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@data", (object)encoded ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@depth", depth);
            cmd.Parameters.AddWithValue("@type", (int)nodeType);
            cmd.Parameters.AddWithValue("@block", _currentBlock);
            cmd.Parameters.AddWithValue("@stem", (object)stem ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            _dirtyHashes[hash] = 0;
        }

        public void RegisterAddressStem(byte[] address, byte[] stemNodeHash)
        {
            if (address == null || stemNodeHash == null) return;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO binary_trie_addr_stems (address, node_hash) VALUES (@addr, @hash)";
            cmd.Parameters.AddWithValue("@addr", address);
            cmd.Parameters.AddWithValue("@hash", stemNodeHash);
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<NodeEntry> GetNodesByDepthRange(int minDepth, int maxDepth)
        {
            var result = new List<NodeEntry>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT node_hash, node_data, depth, node_type, block_number, stem FROM binary_trie_nodes WHERE depth BETWEEN @min AND @max";
            cmd.Parameters.AddWithValue("@min", minDepth);
            cmd.Parameters.AddWithValue("@max", maxDepth);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(ReadNodeEntry(reader));

            return result;
        }

        public IReadOnlyList<NodeEntry> GetStemNodesByAddress(byte[] address)
        {
            if (address == null || address.Length == 0)
                return Array.Empty<NodeEntry>();

            var result = new List<NodeEntry>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = @"SELECT n.node_hash, n.node_data, n.depth, n.node_type, n.block_number, n.stem
                FROM binary_trie_addr_stems s
                INNER JOIN binary_trie_nodes n ON s.node_hash = n.node_hash
                WHERE s.address = @addr";
            cmd.Parameters.AddWithValue("@addr", address);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(ReadNodeEntry(reader));

            return result;
        }

        public IReadOnlyList<NodeEntry> GetDirtyNodes()
        {
            var result = new List<NodeEntry>();
            foreach (var hash in _dirtyHashes.Keys)
            {
                using var cmd = _manager.Connection.CreateCommand();
                cmd.CommandText = "SELECT node_hash, node_data, depth, node_type, block_number, stem FROM binary_trie_nodes WHERE node_hash = @hash";
                cmd.Parameters.AddWithValue("@hash", hash);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    result.Add(ReadNodeEntry(reader));
            }
            return result;
        }

        public void MarkBlockCommitted(long blockNumber)
        {
            _currentBlock = blockNumber;
        }

        public void ClearDirtyTracking()
        {
            _dirtyHashes.Clear();
        }

        public byte[] ExportCheckpoint(int maxDepth)
            => BinaryTrieCheckpointSerializer.Export(GetNodesByDepthRange(0, maxDepth));

        public void ImportCheckpoint(byte[] checkpoint)
        {
            foreach (var entry in BinaryTrieCheckpointSerializer.Import(checkpoint))
                PutNode(entry.Hash, entry.Encoded, entry.Depth, entry.NodeType, entry.Stem);

            ClearDirtyTracking();
        }

        private static NodeEntry ReadNodeEntry(SqliteDataReader reader)
        {
            return new NodeEntry
            {
                Hash = reader["node_hash"] as byte[],
                Encoded = reader["node_data"] as byte[],
                Depth = Convert.ToInt32(reader["depth"]),
                NodeType = (byte)Convert.ToInt32(reader["node_type"]),
                BlockNumber = Convert.ToInt64(reader["block_number"]),
                Stem = reader["stem"] as byte[],
                IsDirty = false
            };
        }
    }
}
