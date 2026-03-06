using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteStorageManager : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly bool _deleteOnDispose;
        private readonly ConcurrentDictionary<int, SqliteConnection> _connections = new();
        private bool _disposed;

        public SqliteStorageManager(string dbPath = null, bool deleteOnDispose = true)
        {
            _deleteOnDispose = deleteOnDispose;

            if (string.IsNullOrEmpty(dbPath))
            {
                var dir = Path.Combine(Path.GetTempPath(), "nethereum-devchain", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                dbPath = Path.Combine(dir, "chain.db");
            }
            else
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }

            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath}";

            var primary = CreateNewConnection();
            _connections[Environment.CurrentManagedThreadId] = primary;

            using var cmd = primary.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS blocks (
                    block_hash TEXT PRIMARY KEY,
                    header_data BLOB NOT NULL
                );

                CREATE TABLE IF NOT EXISTS block_numbers (
                    block_number INTEGER PRIMARY KEY,
                    block_hash TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS metadata (
                    key TEXT PRIMARY KEY,
                    value BLOB
                );

                CREATE TABLE IF NOT EXISTS transactions (
                    tx_hash TEXT PRIMARY KEY,
                    tx_data BLOB NOT NULL,
                    block_hash TEXT NOT NULL,
                    block_number INTEGER NOT NULL,
                    tx_index INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_tx_block_hash ON transactions(block_hash);
                CREATE INDEX IF NOT EXISTS idx_tx_block_number ON transactions(block_number);

                CREATE TABLE IF NOT EXISTS receipts (
                    tx_hash TEXT PRIMARY KEY,
                    receipt_data BLOB NOT NULL,
                    block_hash TEXT NOT NULL,
                    block_number INTEGER NOT NULL,
                    tx_index INTEGER NOT NULL,
                    gas_used BLOB,
                    contract_address TEXT,
                    effective_gas_price BLOB
                );
                CREATE INDEX IF NOT EXISTS idx_receipt_block_hash ON receipts(block_hash);
                CREATE INDEX IF NOT EXISTS idx_receipt_block_number ON receipts(block_number);

                CREATE TABLE IF NOT EXISTS logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    log_data BLOB NOT NULL,
                    address TEXT,
                    block_hash TEXT NOT NULL,
                    block_number INTEGER NOT NULL,
                    tx_hash TEXT NOT NULL,
                    tx_index INTEGER NOT NULL,
                    log_index INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_log_block_number ON logs(block_number);
                CREATE INDEX IF NOT EXISTS idx_log_block_hash ON logs(block_hash);
                CREATE INDEX IF NOT EXISTS idx_log_tx_hash ON logs(tx_hash);
                CREATE INDEX IF NOT EXISTS idx_log_address_block ON logs(address, block_number);

                CREATE TABLE IF NOT EXISTS block_blooms (
                    block_number INTEGER PRIMARY KEY,
                    bloom BLOB NOT NULL
                );

                CREATE TABLE IF NOT EXISTS accounts (
                    address TEXT PRIMARY KEY,
                    account_data BLOB NOT NULL
                );

                CREATE TABLE IF NOT EXISTS account_storage (
                    address TEXT NOT NULL,
                    slot BLOB NOT NULL,
                    value BLOB NOT NULL,
                    PRIMARY KEY (address, slot)
                );

                CREATE TABLE IF NOT EXISTS code (
                    code_hash TEXT PRIMARY KEY,
                    code BLOB NOT NULL
                );

                CREATE TABLE IF NOT EXISTS trie_nodes (
                    node_hash BLOB PRIMARY KEY,
                    node_data BLOB NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }

        public SqliteConnection Connection => GetOrCreateConnection();

        public string DbPath => _dbPath;

        private SqliteConnection GetOrCreateConnection()
        {
            var threadId = Environment.CurrentManagedThreadId;
            return _connections.GetOrAdd(threadId, _ => CreateNewConnection());
        }

        private SqliteConnection CreateNewConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                foreach (var kvp in _connections)
                {
                    kvp.Value?.Close();
                    kvp.Value?.Dispose();
                }
                _connections.Clear();

                if (_deleteOnDispose && File.Exists(_dbPath))
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();
                        File.Delete(_dbPath);
                        var walPath = _dbPath + "-wal";
                        var shmPath = _dbPath + "-shm";
                        if (File.Exists(walPath)) File.Delete(walPath);
                        if (File.Exists(shmPath)) File.Delete(shmPath);

                        var dir = Path.GetDirectoryName(_dbPath);
                        if (dir != null && Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                            Directory.Delete(dir);
                    }
                    catch
                    {
                    }
                }
            }

            _disposed = true;
        }
    }
}
