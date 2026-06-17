using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteStorageManager : IDisposable
    {
        private const string AutoTempDirRoot = "nethereum-devchain";

        private static readonly ConcurrentDictionary<string, byte> s_activeAutoTempDirs = new();

        static SqliteStorageManager()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => WipeRegisteredAutoTempDirs();
        }

        /// <summary>
        /// Best-effort cleanup of <c>%TEMP%/nethereum-devchain</c> subdirectories
        /// left behind by a previous process whose <see cref="Dispose"/> never ran
        /// (SIGKILL / OOM / debugger exit). Only deletes subdirectories whose
        /// creation time predates the current process start — sibling processes
        /// running in parallel keep their live SQLite WAL files. Safe to call at
        /// the top of <c>Main</c> or in an xunit collection fixture. No-op if the
        /// root directory is absent.
        /// </summary>
        public static void PurgeAllAutoTempDirs()
        {
            var root = Path.Combine(Path.GetTempPath(), AutoTempDirRoot);
            if (!Directory.Exists(root)) return;

            DateTime cutoff;
            try
            {
                cutoff = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            }
            catch
            {
                cutoff = DateTime.Now;
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(root);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SqliteStorageManager: failed to enumerate auto-temp root '{root}': {ex.Message}");
                return;
            }

            foreach (var subdir in subdirs)
            {
                try
                {
                    var created = Directory.GetCreationTime(subdir);
                    if (created >= cutoff) continue;
                    Directory.Delete(subdir, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"SqliteStorageManager: failed to delete stale auto-temp dir '{subdir}': {ex.Message}");
                }
            }
        }

        private static void WipeRegisteredAutoTempDirs()
        {
            try { SqliteConnection.ClearAllPools(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SqliteStorageManager: ClearAllPools failed during ProcessExit wipe: {ex.Message}");
            }

            foreach (var dir in s_activeAutoTempDirs.Keys)
            {
                TryDeleteOwnedTempDir(dir);
            }
            s_activeAutoTempDirs.Clear();
        }

        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly bool _deleteOnDispose;
        private readonly string _ownedTempDir;
        private readonly ConcurrentDictionary<int, SqliteConnection> _connections = new();
        private bool _disposed;

        public SqliteStorageManager(string dbPath = null, bool deleteOnDispose = true)
        {
            _deleteOnDispose = deleteOnDispose;

            if (string.IsNullOrEmpty(dbPath))
            {
                _ownedTempDir = Path.Combine(Path.GetTempPath(), AutoTempDirRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_ownedTempDir);
                s_activeAutoTempDirs[_ownedTempDir] = 0;
                dbPath = Path.Combine(_ownedTempDir, "chain.db");
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
                    account_data BLOB NOT NULL,
                    code_hash BLOB
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

                CREATE TABLE IF NOT EXISTS binary_trie_nodes (
                    node_hash BLOB PRIMARY KEY,
                    node_data BLOB NOT NULL,
                    depth INTEGER NOT NULL DEFAULT -1,
                    node_type INTEGER NOT NULL DEFAULT 0,
                    block_number INTEGER NOT NULL DEFAULT 0,
                    stem BLOB
                );
                CREATE INDEX IF NOT EXISTS idx_btn_depth ON binary_trie_nodes(depth);

                CREATE TABLE IF NOT EXISTS binary_trie_addr_stems (
                    address BLOB NOT NULL,
                    node_hash BLOB NOT NULL,
                    PRIMARY KEY (address, node_hash)
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

                if (_deleteOnDispose)
                {
                    SqliteConnection.ClearAllPools();

                    if (_ownedTempDir != null)
                    {
                        TryDeleteOwnedTempDir(_ownedTempDir);
                        s_activeAutoTempDirs.TryRemove(_ownedTempDir, out _);
                    }
                    else
                    {
                        TryDeleteFile(_dbPath);
                        TryDeleteFile(_dbPath + "-wal");
                        TryDeleteFile(_dbPath + "-shm");
                        TryDeleteFile(_dbPath + "-journal");
                    }
                }
            }

            _disposed = true;
        }

        private static void TryDeleteOwnedTempDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SqliteStorageManager: failed to delete owned temp dir '{dir}': {ex.Message}");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"SqliteStorageManager: failed to delete '{path}': {ex.Message}");
            }
        }
    }
}
