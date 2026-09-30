using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainStorageModeTests
    {
        private static string CreateTempDataDir() =>
            Path.Combine(Path.GetTempPath(), "nethereum-devchain-tests", Guid.NewGuid().ToString("N"));

        [Fact]
        public void RocksdbStorageMode_ResolvesPathKeyedRocksDbBundleAtConfiguredDataDir()
        {
            var dataDir = CreateTempDataDir();
            var config = new DevChainServerConfig { Storage = "rocksdb", DataDir = dataDir };
            var services = new ServiceCollection();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            try
            {
                var bundle = provider.GetRequiredService<IChainStoreBundle>();
                var rocksBundle = Assert.IsType<RocksDbChainStoreBundle>(bundle);

                Assert.True(rocksBundle.Rocks.Options.PathKeyedState);
                Assert.Equal(Path.GetFullPath(dataDir), Path.GetFullPath(rocksBundle.Rocks.Options.DatabasePath));
                Assert.True(Directory.Exists(dataDir));
            }
            finally
            {
                (provider.GetService<IChainStoreBundle>() as IDisposable)?.Dispose();
                TryDeleteDirectory(dataDir);
            }
        }

        [Fact]
        public async Task RocksdbStorageMode_HostedServiceStopReleasesRocksDbLockForReopen()
        {
            var dataDir = CreateTempDataDir();
            var config = new DevChainServerConfig { Storage = "rocksdb", DataDir = dataDir };
            var services = new ServiceCollection();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            var node = provider.GetRequiredService<DevChainNode>();
            var accountManager = provider.GetRequiredService<DevAccountManager>();
            var hosted = new DevChainHostedService(node, accountManager, provider);

            await hosted.StopAsync(CancellationToken.None);

            using var reopened = RocksDbChainStoreBundle.Open(dataDir);
            Assert.NotNull(reopened);

            TryDeleteDirectory(dataDir);
        }

        [Fact]
        public void SqliteStorageMode_StillResolvesWorkingBundle()
        {
            var config = new DevChainServerConfig { Storage = "sqlite", Persist = false };
            var services = new ServiceCollection();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var devChainBundle = Assert.IsType<DevChainChainStoreBundle>(bundle);

            Assert.NotNull(devChainBundle.Blocks);
            Assert.NotNull(devChainBundle.State);

            (bundle as IDisposable)?.Dispose();
        }

        [Fact]
        public void MemoryStorageMode_StillResolvesWorkingBundle()
        {
            var config = new DevChainServerConfig { Storage = "memory" };
            var services = new ServiceCollection();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var devChainBundle = Assert.IsType<DevChainChainStoreBundle>(bundle);

            Assert.NotNull(devChainBundle.Blocks);
            Assert.NotNull(devChainBundle.State);

            (bundle as IDisposable)?.Dispose();
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
