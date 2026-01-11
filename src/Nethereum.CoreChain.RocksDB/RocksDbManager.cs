using System;
using System.Collections.Generic;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB
{
    public class RocksDbManager : IDisposable
    {
        public const string CF_BLOCKS = "blocks";
        public const string CF_BLOCK_NUMBERS = "block_numbers";
        public const string CF_TRANSACTIONS = "transactions";
        public const string CF_TX_BY_BLOCK = "tx_by_block";
        public const string CF_RECEIPTS = "receipts";
        public const string CF_LOGS = "logs";
        public const string CF_LOG_BY_BLOCK = "log_by_block";
        public const string CF_LOG_BY_ADDRESS = "log_by_address";
        public const string CF_STATE_ACCOUNTS = "state_accounts";
        public const string CF_STATE_STORAGE = "state_storage";
        public const string CF_STATE_CODE = "state_code";
        public const string CF_TRIE_NODES = "trie_nodes";
        public const string CF_FILTERS = "filters";
        public const string CF_METADATA = "metadata";

        private static readonly string[] ColumnFamilyNames = new[]
        {
            CF_BLOCKS,
            CF_BLOCK_NUMBERS,
            CF_TRANSACTIONS,
            CF_TX_BY_BLOCK,
            CF_RECEIPTS,
            CF_LOGS,
            CF_LOG_BY_BLOCK,
            CF_LOG_BY_ADDRESS,
            CF_STATE_ACCOUNTS,
            CF_STATE_STORAGE,
            CF_STATE_CODE,
            CF_TRIE_NODES,
            CF_FILTERS,
            CF_METADATA
        };

        private readonly RocksDb _database;
        private readonly Dictionary<string, ColumnFamilyHandle> _columnFamilies;
        private readonly RocksDbStorageOptions _options;
        private bool _disposed;

        public RocksDbManager(RocksDbStorageOptions options)
        {
            _options = options ?? new RocksDbStorageOptions();
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

        public void Write(WriteBatch batch, WriteOptions writeOptions = null)
        {
            _database.Write(batch, writeOptions);
        }

        public byte[] Get(string columnFamily, byte[] key, ReadOptions readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            return _database.Get(key, cf, readOptions);
        }

        public void Put(string columnFamily, byte[] key, byte[] value, WriteOptions writeOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            _database.Put(key, value, cf, writeOptions);
        }

        public void Delete(string columnFamily, byte[] key, WriteOptions writeOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            _database.Remove(key, cf, writeOptions);
        }

        public bool KeyExists(string columnFamily, byte[] key, ReadOptions readOptions = null)
        {
            var cf = GetColumnFamily(columnFamily);
            var value = _database.Get(key, cf, readOptions);
            return value != null;
        }

        public Iterator CreateIterator(string columnFamily, ReadOptions readOptions = null)
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
                _database.CompactRange((byte[])null, (byte[])null, cf);
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
