using RocksDbSharp;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB
{
    public class BundlerRocksDbManager : IDisposable
    {
        public const string CF_USEROP_PENDING = "userop_pending";
        public const string CF_USEROP_SUBMITTED = "userop_submitted";
        public const string CF_USEROP_INCLUDED = "userop_included";
        public const string CF_USEROP_FAILED = "userop_failed";
        public const string CF_SENDER_INDEX = "sender_index";
        public const string CF_TX_MAPPING = "tx_mapping";
        public const string CF_REPUTATION = "reputation";
        public const string CF_METADATA = "metadata";

        private static readonly string[] ColumnFamilyNames = new[]
        {
            CF_USEROP_PENDING,
            CF_USEROP_SUBMITTED,
            CF_USEROP_INCLUDED,
            CF_USEROP_FAILED,
            CF_SENDER_INDEX,
            CF_TX_MAPPING,
            CF_REPUTATION,
            CF_METADATA
        };

        private readonly RocksDb _database;
        private readonly Dictionary<string, ColumnFamilyHandle> _columnFamilies;
        private readonly BundlerRocksDbOptions _options;
        private bool _disposed;

        public BundlerRocksDbManager(BundlerRocksDbOptions options)
        {
            _options = options ?? new BundlerRocksDbOptions();
            _columnFamilies = new Dictionary<string, ColumnFamilyHandle>();

            var dbOptions = CreateDbOptions();
            var cfOptions = CreateColumnFamilyOptions();

            var columnFamilies = new ColumnFamilies();
            foreach (var cfName in ColumnFamilyNames)
            {
                columnFamilies.Add(cfName, cfOptions);
            }

            _database = RocksDb.Open(dbOptions, _options.DatabasePath, columnFamilies);

            foreach (var cfName in ColumnFamilyNames)
            {
                _columnFamilies[cfName] = _database.GetColumnFamily(cfName);
            }
        }

        public RocksDb Database => _database;

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

        public Snapshot CreateSnapshot()
        {
            return _database.CreateSnapshot();
        }

        public void Write(WriteBatch batch, WriteOptions? writeOptions = null)
        {
            _database.Write(batch, writeOptions);
        }

        public byte[]? Get(string columnFamily, byte[] key, ReadOptions? readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            return _database.Get(key, cf, readOptions);
        }

        public void Put(string columnFamily, byte[] key, byte[] value, WriteOptions? writeOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            _database.Put(key, value, cf, writeOptions);
        }

        public void Delete(string columnFamily, byte[] key, WriteOptions? writeOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            _database.Remove(key, cf, writeOptions);
        }

        public bool KeyExists(string columnFamily, byte[] key, ReadOptions? readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            var value = _database.Get(key, cf, readOptions);
            return value != null;
        }

        public Iterator CreateIterator(string columnFamily, ReadOptions? readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            return _database.NewIterator(cf, readOptions);
        }

        public void Flush()
        {
            _database.Flush(new FlushOptions());
        }

        public void Compact()
        {
            foreach (var cf in _columnFamilies.Values)
            {
                _database.CompactRange((byte[]?)null, (byte[]?)null, cf);
            }
        }

        private DbOptions CreateDbOptions()
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true)
                .SetMaxBackgroundCompactions(_options.MaxBackgroundCompactions)
                .SetMaxBackgroundFlushes(_options.MaxBackgroundFlushes);

            if (_options.EnableStatistics)
            {
                options.EnableStatistics();
            }

            return options;
        }

        private ColumnFamilyOptions CreateColumnFamilyOptions()
        {
            var blockBasedOptions = new BlockBasedTableOptions()
                .SetBlockCache(Cache.CreateLru((ulong)_options.BlockCacheSize))
                .SetFilterPolicy(BloomFilterPolicy.Create(_options.BloomFilterBitsPerKey));

            var cfOptions = new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(blockBasedOptions)
                .SetWriteBufferSize((ulong)_options.WriteBufferSize)
                .SetMaxWriteBufferNumber(_options.MaxWriteBufferNumber)
                .SetCompression(Compression.Lz4);

            return cfOptions;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _database?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
