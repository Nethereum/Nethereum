using System;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainConfig = Nethereum.AppChain.AppChainConfig;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class SequencerTestFixture : IDisposable
    {
        public string DatabasePath { get; }
        public RocksDbManager Manager { get; private set; } = null!;
        public IBlockStore BlockStore { get; private set; } = null!;
        public ITransactionStore TransactionStore { get; private set; } = null!;
        public IReceiptStore ReceiptStore { get; private set; } = null!;
        public ILogStore LogStore { get; private set; } = null!;
        public IStateStore StateStore { get; private set; } = null!;

        public SequencerTestFixture()
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), $"sequencer_test_{Guid.NewGuid():N}");
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
        }

        public AppChainCore CreateAppChain(AppChainConfig? config = null)
        {
            return new AppChainCore(
                config ?? AppChainConfig.Default,
                BlockStore,
                TransactionStore,
                ReceiptStore,
                LogStore,
                StateStore);
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
