using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;


namespace Nethereum.ChainNode.Hosting
{
    public sealed class ChainNodeStorage : IAsyncDisposable
    {
        private readonly RocksDbManager _manager;

        private ChainNodeStorage(IChainStoreBundle bundle, RocksDbManager manager)
        {
            Bundle = bundle;
            _manager = manager;
        }

        public IChainStoreBundle Bundle { get; }

        public RocksDbManager Manager => _manager;

        public static ChainNodeStorage FromBundle(IChainStoreBundle bundle, RocksDbManager manager = null) =>
            new ChainNodeStorage(bundle, manager);

        public static ChainNodeStorage Open(
            ChainNodeConfig config, ILogger logger = null,
            Nethereum.Model.ITransactionVerificationAndRecovery signer = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return config.Storage.InMemory
                ? OpenInMemory(config, logger)
                : OpenRocksDb(config, logger, signer);
        }

        public static RocksDbStorageOptions BuildStorageOptions(ChainNodeStorageConfig storage) =>
            new RocksDbStorageOptions
            {
                DatabasePath = storage.DataDirectory,
                PathKeyedState = storage.PathKeyedState,
                TrieNodeHistoryBlocks = storage.TrieNodeHistoryBlocks,
                TrieNodeHistoryIndex = storage.TrieNodeHistoryIndex,
                BlockCacheSize = storage.BlockCacheSize,
                SplitHistoryStore = storage.SplitHistoryStore,
                HotWindowBlocks = storage.HotWindowBlocks,
                EnableLogIndex = storage.EnableLogIndex,
                PromotionEnabled = storage.PromotionEnabled,
                UseFreezerHistory = storage.UseFreezerHistory,
                FreezerHistoryDirectory = storage.FreezerHistoryDirectory,
                BackgroundFreezeIndexing = storage.BackgroundFreezeIndexing,
                FreezerBackgroundDegreeOfParallelism = storage.FreezerBackgroundDegreeOfParallelism,
            };

        public static HistoricalStateOptions BuildJournalOptions(int journalBlocks)
        {
            if (journalBlocks < 0) return null;
            if (journalBlocks == 0) return HistoricalStateOptions.FullArchive;

            return new HistoricalStateOptions
            {
                MaxHistoryBlocks = journalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, journalBlocks / 16),
            };
        }

        private static ChainNodeStorage OpenInMemory(ChainNodeConfig config, ILogger logger)
        {
            logger?.LogInformation("Storage: in-memory");

            return new ChainNodeStorage(
                InMemoryChainStoreBundle.Open(BuildJournalOptions(config.Storage.JournalBlocks)),
                manager: null);
        }

        private static ChainNodeStorage OpenRocksDb(
            ChainNodeConfig config, ILogger logger, Nethereum.Model.ITransactionVerificationAndRecovery signer)
        {
            var dataDirectory = config.Storage.DataDirectory;

            var bundle = RocksDbChainStoreBundle.Open(
                dataDirectory,
                BuildJournalOptions(config.Storage.JournalBlocks),
                bulkSync: config.Sync.BulkSync,
                storageOptions: BuildStorageOptions(config.Storage),
                signer: signer);

            logger?.LogInformation(
                "Storage: RocksDB at {DataDir} (journal_blocks={Journal}, path_keyed={PathKeyed}, " +
                "trie_node_history_blocks={NodeHistory}, split_history_store={Split})",
                dataDirectory, config.Storage.JournalBlocks, config.Storage.PathKeyedState,
                config.Storage.TrieNodeHistoryBlocks, config.Storage.SplitHistoryStore);

            return new ChainNodeStorage(bundle, bundle.Rocks);
        }

        public async ValueTask DisposeAsync()
        {
            if (Bundle is IAsyncDisposable asyncBundle) await asyncBundle.DisposeAsync().ConfigureAwait(false);
            else (Bundle as IDisposable)?.Dispose();

            _manager?.Dispose();
        }
    }
}
