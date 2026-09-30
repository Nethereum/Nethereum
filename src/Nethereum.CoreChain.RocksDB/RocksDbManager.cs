using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Nethereum.CoreChain.RocksDB.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB
{
    public enum CatalogueScope { Both, Core, History, FreezerHistory }

    public class RocksDbManager : IDisposable
    {
        public const string CF_BLOCKS = "blocks";
        public const string CF_BLOCK_NUMBERS = "block_numbers";
        public const string CF_TRANSACTIONS = "transactions";
        public const string CF_TX_BY_BLOCK = "tx_by_block";
        public const string CF_UNCLES = "uncles";
        public const string CF_WITHDRAWALS = "withdrawals";
        public const string CF_RECEIPTS = "receipts";
        public const string CF_LOGS = "logs";
        public const string CF_LOG_BY_BLOCK = "log_by_block";
        public const string CF_LOG_BY_ADDRESS = "log_by_address";
        public const string CF_STATE_ACCOUNTS = "state_accounts";
        public const string CF_STATE_STORAGE = "state_storage";
        public const string CF_STATE_CODE = "state_code";
        public const string CF_TRIE_NODES = "trie_nodes";
        public const string CF_STATE_TRIE_ACCOUNT = "state_trie_account";
        public const string CF_STATE_TRIE_STORAGE = "state_trie_storage";
        public const string CF_NODE_HISTORY = "node_band_log";
        public const string CF_NODE_HISTORY_INDEX = "node_history_index";
        public const string CF_STATE_ROOT_INDEX = "state_root_index";
        public const string CF_BINARY_TRIE_NODES = "binary_trie_nodes";
        public const string CF_BINARY_TRIE_DEPTH_IDX = "binary_trie_depth_idx";
        public const string CF_BINARY_TRIE_ADDR_STEMS = "binary_trie_addr_stems";
        public const string CF_FILTERS = "filters";
        public const string CF_METADATA = "metadata";
        public const string CF_BLOCK_BLOOMS = "block_blooms";
        public const string CF_RECEIPT_BY_BLOCK = "receipt_by_block";
        public const string CF_LOG_BY_TX = "log_by_tx";
        public const string CF_MSG_RESULTS = "msg_results";
        public const string CF_MSG_RESULTS_BY_LEAF = "msg_results_by_leaf";
        public const string CF_STATE_HISTORY_ACCOUNTS = "state_history_accounts";
        public const string CF_STATE_HISTORY_STORAGE = "state_history_storage";
        public const string CF_STATE_HISTORY_BLOCK_INDEX = "state_history_block_index";
        public const string CF_STATE_HISTORY_META = "state_history_meta";

        public const string CF_HOT_BLOCK_HEADER = "hot_block_header";
        public const string CF_HOT_BLOCK_META = "hot_block_meta";
        public const string CF_HOT_BLOCK_HASH_INDEX = "hot_block_hash_index";
        public const string CF_HOT_TX_BODY = "hot_tx_body";
        public const string CF_HOT_TX_HASH_INDEX = "hot_tx_hash_index";
        public const string CF_HOT_RECEIPT_BODY = "hot_receipt_body";
        public const string CF_HOT_BLOCK_ACCESS_LIST = "hot_block_access_list";

        public static readonly IReadOnlyList<string> StateTrieCfs = new[]
        {
            CF_STATE_ACCOUNTS, CF_STATE_STORAGE, CF_STATE_CODE,
            CF_TRIE_NODES, CF_STATE_TRIE_ACCOUNT, CF_STATE_TRIE_STORAGE,
        };
        public static readonly IReadOnlyList<string> BinaryTrieCfs = new[]
        {
            CF_BINARY_TRIE_NODES, CF_BINARY_TRIE_DEPTH_IDX, CF_BINARY_TRIE_ADDR_STEMS,
        };
        public static readonly IReadOnlyList<string> StateHistoryCfs = new[]
        {
            CF_STATE_HISTORY_ACCOUNTS, CF_STATE_HISTORY_STORAGE,
            CF_STATE_HISTORY_BLOCK_INDEX, CF_STATE_HISTORY_META,
        };
        public static readonly IReadOnlyList<string> NodeHistoryCfs = new[]
        {
            CF_NODE_HISTORY, CF_NODE_HISTORY_INDEX, CF_STATE_ROOT_INDEX,
        };
        public static readonly IReadOnlyList<string> LogReceiptCfs = new[]
        {
            CF_RECEIPTS, CF_LOGS, CF_LOG_BY_BLOCK, CF_LOG_BY_ADDRESS,
            CF_LOG_BY_TX, CF_RECEIPT_BY_BLOCK, CF_BLOCK_BLOOMS,
        };
        public static readonly IReadOnlyList<string> AuxiliaryStateCfs = new[]
        {
            CF_FILTERS, CF_MSG_RESULTS, CF_MSG_RESULTS_BY_LEAF,
        };

        public static IReadOnlyList<string> AllLiveColumnFamilyNames { get; } =
            LiveColumnFamilies.Catalogue.Select(c => c.Name).ToArray();

        public static IReadOnlyList<string> ColumnFamilyNames => AllLiveColumnFamilyNames;

        private readonly RocksDb _database;
        private readonly Dictionary<string, ColumnFamilyHandle> _columnFamilies;
        private readonly RocksDbStorageOptions _options;
        private readonly CatalogueScope _scope;
        private Cache _sharedCache;

        public RocksDbStorageOptions Options => _options;

        public CatalogueScope Scope => _scope;

        public IReadOnlyList<string> OpenColumnFamilyNames { get; }

        private IntPtr _rateLimiter = IntPtr.Zero;
        private bool _disposed;

        private readonly object _compactionGate = new object();

        private readonly ReaderWriterLockSlim _writeDisposeGate = new ReaderWriterLockSlim();

        public RocksDbManager(RocksDbStorageOptions options) : this(options, CatalogueScope.Both) { }

        private static bool ShouldOpenHotWindowCatalogue(CatalogueScope scope, RocksDbStorageOptions options)
            => scope == CatalogueScope.Core || (scope == CatalogueScope.Both && options.PromotionEnabled);

        public RocksDbManager(RocksDbStorageOptions options, CatalogueScope scope)
        {
            _options = options ?? new RocksDbStorageOptions();
            _scope = scope;
            _columnFamilies = new Dictionary<string, ColumnFamilyHandle>();

            var dbOptions = CreateDbOptions();
            try
            {
                var sharedCache = Cache.CreateLru((ulong)_options.BlockCacheSize);
                _sharedCache = sharedCache;
                var columnFamilies = new ColumnFamilies();
                var openNames = new List<string>();

                if (scope == CatalogueScope.Both || scope == CatalogueScope.Core)
                {
                    foreach (var (name, profile) in LiveColumnFamilies.Catalogue)
                    {
                        columnFamilies.Add(name, RocksProfiles.ForLive(profile, sharedCache, _options.Preset));
                        openNames.Add(name);
                    }
                }

                if (ShouldOpenHotWindowCatalogue(scope, _options))
                {
                    foreach (var (name, profile) in HotWindowColumnFamilies.Catalogue)
                    {
                        columnFamilies.Add(name, RocksProfiles.ForLive(profile, sharedCache, _options.Preset));
                        openNames.Add(name);
                    }
                }

                if (scope == CatalogueScope.Both || scope == CatalogueScope.History)
                {
                    foreach (var (name, profile) in HistoryColumnFamilies.Catalogue)
                    {
                        columnFamilies.Add(name, RocksProfiles.ForHistoryProfile(profile, sharedCache));
                        openNames.Add(name);
                    }
                }

                if (scope == CatalogueScope.FreezerHistory)
                {
                    foreach (var (name, profile) in HistoryColumnFamilies.FreezerHistoryCatalogue)
                    {
                        columnFamilies.Add(name, RocksProfiles.ForHistoryProfile(profile, sharedCache));
                        openNames.Add(name);
                    }
                }

                _database = RocksDb.Open(dbOptions, _options.DatabasePath, columnFamilies);

                foreach (var cfName in openNames)
                {
                    _columnFamilies[cfName] = _database.GetColumnFamily(cfName);
                }
                OpenColumnFamilyNames = openNames;
            }
            catch
            {
                RocksProfiles.DestroyRateLimiter(_rateLimiter);
                _rateLimiter = IntPtr.Zero;
                throw;
            }
        }

        public bool HasColumnFamily(string name) => _columnFamilies.ContainsKey(name);

        public RocksDb Database => _database;

        public readonly struct RocksDbLease : IDisposable
        {
            private readonly RocksDbManager _owner;

            internal RocksDbLease(RocksDbManager owner)
            {
                _owner = owner;
            }

            public RocksDb Database => _owner._database;

            public void Dispose() => _owner.LeaveLiveDatabase();
        }

        public RocksDbLease Lease()
        {
            EnterLiveDatabase();
            return new RocksDbLease(this);
        }

        public ColumnFamilyHandle GetColumnFamily(string name)
        {
            if (_columnFamilies.TryGetValue(name, out var handle))
            {
                return handle;
            }
            throw new ArgumentException($"Column family '{name}' not found");
        }

        public WriteBatch CreateWriteBatch()
        {
            return new WriteBatch();
        }

        public static byte[] Write64BE(ulong v)
        {
            var b = new byte[8];
            for (int i = 7; i >= 0; i--) { b[i] = (byte)(v & 0xff); v >>= 8; }
            return b;
        }

        public static ulong Read64BE(byte[] b)
        {
            ulong v = 0;
            for (int i = 0; i < 8; i++) v = (v << 8) | b[i];
            return v;
        }

        public Snapshot CreateSnapshot()
        {
            EnterLiveDatabase();
            try
            {
                return _database.CreateSnapshot();
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        private long _writeCallCount;
        private long _putCallCount;
        private long _deleteCallCount;

        public long WriteCallCount => System.Threading.Interlocked.Read(ref _writeCallCount);
        public long PutCallCount => System.Threading.Interlocked.Read(ref _putCallCount);
        public long DeleteCallCount => System.Threading.Interlocked.Read(ref _deleteCallCount);
        public long NativeCommitCount => WriteCallCount + PutCallCount + DeleteCallCount;

        private void EnterLiveDatabase()
        {
            _writeDisposeGate.EnterReadLock();
            if (_disposed)
            {
                _writeDisposeGate.ExitReadLock();
                throw new ObjectDisposedException(nameof(RocksDbManager));
            }
        }

        private void LeaveLiveDatabase() => _writeDisposeGate.ExitReadLock();

        public virtual void Write(WriteBatch batch, WriteOptions writeOptions = null)
        {
            EnterLiveDatabase();
            try
            {
                System.Threading.Interlocked.Increment(ref _writeCallCount);
                _database.Write(batch, writeOptions);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public byte[] Get(string columnFamily, byte[] key, ReadOptions readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            EnterLiveDatabase();
            try
            {
                return _database.Get(key, cf, readOptions);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public void Put(string columnFamily, byte[] key, byte[] value, WriteOptions writeOptions = null)
        {
            System.Threading.Interlocked.Increment(ref _putCallCount);
            var cf = GetColumnFamily(columnFamily);
            EnterLiveDatabase();
            try
            {
                _database.Put(key, value, cf, writeOptions);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public void Delete(string columnFamily, byte[] key, WriteOptions writeOptions = null)
        {
            System.Threading.Interlocked.Increment(ref _deleteCallCount);
            var cf = GetColumnFamily(columnFamily);
            EnterLiveDatabase();
            try
            {
                _database.Remove(key, cf, writeOptions);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public bool KeyExists(string columnFamily, byte[] key, ReadOptions readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            EnterLiveDatabase();
            try
            {
                return _database.Get(key, cf, readOptions) != null;
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public Iterator CreateIterator(string columnFamily, ReadOptions readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            EnterLiveDatabase();
            try
            {
                return _database.NewIterator(cf, readOptions);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public void WipeColumnFamily(string columnFamily)
        {
            var cfOptions = BuildColumnFamilyOptions(columnFamily);
            EnterLiveDatabase();
            try
            {
                _database.DropColumnFamily(columnFamily);
                _columnFamilies[columnFamily] = _database.CreateColumnFamily(cfOptions, columnFamily);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        private ColumnFamilyOptions BuildColumnFamilyOptions(string columnFamily)
        {
            foreach (var (name, profile) in LiveColumnFamilies.Catalogue)
                if (name == columnFamily) return RocksProfiles.ForLive(profile, _sharedCache, _options.Preset);
            foreach (var (name, profile) in HotWindowColumnFamilies.Catalogue)
                if (name == columnFamily) return RocksProfiles.ForLive(profile, _sharedCache, _options.Preset);
            foreach (var (name, profile) in HistoryColumnFamilies.Catalogue)
                if (name == columnFamily)
                    return RocksProfiles.ForHistoryProfile(profile, _sharedCache);
            throw new ArgumentException($"No column-family profile for '{columnFamily}'", nameof(columnFamily));
        }

        public void Flush()
        {
            EnterLiveDatabase();
            try
            {
                _database.Flush(new FlushOptions());
            }
            catch (RocksDbException ex) when (IsTransientFlushStall(ex.Message))
            {
                throw new Merkle.Patricia.Storage.TransientFlushUnavailableException(
                    "RocksDB manual flush unavailable (writes transiently stopped); data remains WAL-durable.", ex);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        public static bool IsTransientFlushStall(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("Writes have been stopped", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("unable to perform manual flush", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("try again later", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void CreateDatabaseCheckpoint(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentException("Output path required", nameof(outputPath));
            if (System.IO.Directory.Exists(outputPath))
                throw new ArgumentException($"Output path already exists: {outputPath}", nameof(outputPath));
            EnterLiveDatabase();
            try
            {
                _database.Flush(new FlushOptions());
                using var cp = _database.Checkpoint();
                cp.Save(outputPath, 0);
            }
            finally
            {
                LeaveLiveDatabase();
            }
        }

        private const int DefaultCompactionSubcompactions = 4;

        public void Compact()
        {
            foreach (var name in _columnFamilies.Keys)
                CompactColumnFamilyParallel(name, DefaultCompactionSubcompactions);
        }

        public void CompactColumnFamily(string columnFamily)
            => CompactColumnFamilyParallel(columnFamily, DefaultCompactionSubcompactions);

        public void CompactColumnFamilyParallel(string columnFamily, int subcompactions)
        {
            lock (_compactionGate)
            {
                if (_disposed) return;
                CompactRangeCfParallel(_database, GetColumnFamily(columnFamily), subcompactions);
            }
        }

        internal static void CompactRangeCfParallel(RocksDb database, ColumnFamilyHandle cf, int subcompactions)
        {
            IntPtr opt = IntPtr.Zero;
            try
            {
                opt = Native.Instance.rocksdb_compactoptions_create();
                Native.Instance.rocksdb_compactoptions_set_max_subcompactions(opt, Math.Max(1, subcompactions));
                Native.Instance.rocksdb_compactoptions_set_exclusive_manual_compaction(opt, false);
                Native.Instance.rocksdb_compactoptions_set_allow_write_stall(opt, true);
                Native.Instance.rocksdb_compact_range_cf_opt(
                    database.Handle, cf.Handle, opt,
                    (byte[])null, UIntPtr.Zero, (byte[])null, UIntPtr.Zero);
            }
            catch (EntryPointNotFoundException)
            {
                database.CompactRange((byte[])null, (byte[])null, cf);
            }
            finally
            {
                if (opt != IntPtr.Zero)
                {
                    try { Native.Instance.rocksdb_compactoptions_destroy(opt); } catch { }
                }
            }
        }

        private DbOptions CreateDbOptions()
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true)
                .SetMaxBackgroundCompactions(_options.MaxBackgroundCompactions)
                .SetMaxBackgroundFlushes(_options.MaxBackgroundFlushes)
                .SetMaxTotalWalSize((ulong)_options.MaxTotalWalSize)
                .SetBytesPerSync((ulong)_options.BytesPerSync)
                .SetDbWriteBufferSize((ulong)_options.DbWriteBufferSize)
                .SetMaxOpenFiles(_options.MaxOpenFiles);

            if (_options.EnableStatistics)
            {
                options.EnableStatistics();
            }

            if (_options.MaxSubcompactions > 1)
            {
                try { Native.Instance.rocksdb_options_set_max_subcompactions(options.Handle, (uint)_options.MaxSubcompactions); }
                catch { }
            }

            if (_options.MaxBackgroundJobs > 0)
            {
                try { Native.Instance.rocksdb_options_set_max_background_jobs(options.Handle, _options.MaxBackgroundJobs); }
                catch { }
            }

            if (_options.RateLimiterEnabled)
            {
                _rateLimiter = RocksProfiles.CreateAutoTunedRateLimiter(_options.RateLimiterBytesPerSecond);
                RocksProfiles.AttachRateLimiter(options, _rateLimiter);
            }

            return options;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_compactionGate)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (disposing)
                    {
                        _writeDisposeGate.EnterWriteLock();
                        try
                        {
                            _database?.Dispose();
                            RocksProfiles.DestroyRateLimiter(_rateLimiter);
                            _rateLimiter = IntPtr.Zero;
                        }
                        finally
                        {
                            _writeDisposeGate.ExitWriteLock();
                        }
                    }
                }
            }
            if (disposing) _writeDisposeGate.Dispose();
        }
    }
}
