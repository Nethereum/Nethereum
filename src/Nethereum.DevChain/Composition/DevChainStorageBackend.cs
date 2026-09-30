using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Storage.Sqlite;

namespace Nethereum.DevChain.Composition
{
    public static class DevChainStorageBackend
    {
        public static (IChainStoreBundle Bundle, IAsyncDisposable Handle) Open(DevChainServerConfig config)
        {
            var storageMode = config.Storage?.ToLowerInvariant() ?? "sqlite";

            if (storageMode == "memory")
            {
                var bundle = DevChainChainStoreBundle.OpenInMemory();
                return (bundle, new BundleHandle(bundle));
            }

            if (storageMode == "rocksdb")
            {
                var nodeConfig = new ChainNodeConfig();
                nodeConfig.Storage.DataDirectory = config.DataDir;
                nodeConfig.Storage.PathKeyedState = true;

                var storage = ChainNodeStorage.Open(nodeConfig);
                return (storage.Bundle, storage);
            }

            var dbPath = config.Persist ? Path.Combine(config.DataDir, "chain.db") : null;
            var sqliteManager = new SqliteStorageManager(dbPath, deleteOnDispose: !config.Persist);
            var sqliteBundle = DevChainChainStoreBundle.OpenSqlite(sqliteManager);
            return (sqliteBundle, new BundleAndManagerHandle(sqliteBundle, sqliteManager));
        }

        private sealed class BundleHandle : IAsyncDisposable
        {
            private readonly IChainStoreBundle _bundle;

            public BundleHandle(IChainStoreBundle bundle) => _bundle = bundle;

            public ValueTask DisposeAsync() => _bundle.DisposeAsync();
        }

        private sealed class BundleAndManagerHandle : IAsyncDisposable
        {
            private readonly IChainStoreBundle _bundle;
            private readonly IDisposable _manager;

            public BundleAndManagerHandle(IChainStoreBundle bundle, IDisposable manager)
            {
                _bundle = bundle;
                _manager = manager;
            }

            public async ValueTask DisposeAsync()
            {
                await _bundle.DisposeAsync().ConfigureAwait(false);
                _manager.Dispose();
            }
        }
    }
}
