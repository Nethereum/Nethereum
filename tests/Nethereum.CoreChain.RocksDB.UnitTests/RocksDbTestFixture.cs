using System;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbTestFixture : IDisposable
    {
        public string DatabasePath { get; }
        public RocksDbManager Manager { get; private set; }
        public IBlockStore BlockStore { get; private set; }
        public ITransactionStore TransactionStore { get; private set; }
        public IReceiptStore ReceiptStore { get; private set; }
        public ILogStore LogStore { get; private set; }
        public IStateStore StateStore { get; private set; }
        public IFilterStore FilterStore { get; private set; }
        public ITrieNodeStore TrieNodeStore { get; private set; }

        public RocksDbTestFixture()
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid():N}");
            Initialize();
        }

        private void Initialize()
        {
            var options = new RocksDbStorageOptions
            {
                DatabasePath = DatabasePath
            };

            Manager = new RocksDbManager(options);
            BlockStore = new RocksDbBlockStore(Manager);
            TransactionStore = new RocksDbTransactionStore(Manager, BlockStore);
            ReceiptStore = new RocksDbReceiptStore(Manager, BlockStore);
            LogStore = new RocksDbLogStore(Manager);
            StateStore = new RocksDbStateStore(Manager);
            FilterStore = new RocksDbFilterStore(Manager);
            TrieNodeStore = new RocksDbTrieNodeStore(Manager);
        }

        public void Dispose()
        {
            Manager?.Dispose();
            if (Directory.Exists(DatabasePath))
            {
                try
                {
                    Directory.Delete(DatabasePath, true);
                }
                catch
                {
                }
            }
        }
    }
}
