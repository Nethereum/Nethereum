using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeStorageOpenFaithfulnessTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"cnstoragefaithful_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string NewDataDir(string name) => Path.Combine(_root, name);

        private static ChainNodeConfig RocksDbConfig(string dataDir)
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = false;
            config.Storage.DataDirectory = dataDir;
            return config;
        }

        [Fact]
        public async Task Given_FreezerBackgroundDegreeOfParallelismSet_When_ChainNodeStorageOpensRocksDb_Then_ItReachesTheOpenedRocksDbManagerOptions()
        {
            var config = RocksDbConfig(NewDataDir("parallelism"));
            config.Storage.FreezerBackgroundDegreeOfParallelism = 7;

            await using var storage = ChainNodeStorage.Open(config);

            Assert.NotNull(storage.Manager);
            Assert.Equal(7, storage.Manager.Options.FreezerBackgroundDegreeOfParallelism);
        }

        [Fact]
        public async Task Given_FreezerBackgroundDegreeOfParallelismUnset_When_ChainNodeStorageOpensRocksDb_Then_ItStaysAtTheLibraryDefaultOfZero()
        {
            var config = RocksDbConfig(NewDataDir("parallelism-default"));

            await using var storage = ChainNodeStorage.Open(config);

            Assert.Equal(0, storage.Manager.Options.FreezerBackgroundDegreeOfParallelism);
        }

        [Fact]
        public async Task Given_SplitHistoryStore_When_ChainNodeStorageOpens_Then_ItOpensTwoPhysicalRocksDbDatabasesNotOne()
        {
            var dataDir = NewDataDir("split");
            var config = RocksDbConfig(dataDir);
            config.Storage.SplitHistoryStore = true;

            await using var storage = ChainNodeStorage.Open(config);

            var coreDir = Path.Combine(dataDir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dataDir, RocksDbChainStoreBundle.HistorySubDir);
            Assert.True(File.Exists(Path.Combine(coreDir, "CURRENT")));
            Assert.True(File.Exists(Path.Combine(historyDir, "CURRENT")));
            Assert.Equal(coreDir, storage.Manager.Options.DatabasePath);
            Assert.True(storage.Bundle is RocksDbChainStoreBundle);
        }

        [Fact]
        public async Task Given_SplitHistoryStoreFalse_When_ChainNodeStorageOpens_Then_ItOpensOnlyTheSingleRootDatabase()
        {
            var dataDir = NewDataDir("nonsplit");
            var config = RocksDbConfig(dataDir);

            await using var storage = ChainNodeStorage.Open(config);

            Assert.True(File.Exists(Path.Combine(dataDir, "CURRENT")));
            var coreDir = Path.Combine(dataDir, RocksDbChainStoreBundle.CoreSubDir);
            Assert.False(Directory.Exists(coreDir));
            Assert.Equal(dataDir, storage.Manager.Options.DatabasePath);
        }

        [Fact]
        public async Task Given_UseFreezerHistoryWithASigner_When_ChainNodeStorageOpens_Then_ItOpensWithoutThrowing()
        {
            var dataDir = NewDataDir("freezer");
            var config = RocksDbConfig(dataDir);
            config.Storage.UseFreezerHistory = true;
            config.Storage.FreezerHistoryDirectory = Path.Combine(dataDir, "freezer-archive");

            await using var storage = ChainNodeStorage.Open(config, signer: new TransactionVerificationAndRecoveryImp());

            var bundle = Assert.IsType<RocksDbChainStoreBundle>(storage.Bundle);
            Assert.Equal(0, bundle.FreezerHead);
        }

        [Fact]
        public async Task Given_UseFreezerHistoryWithoutASigner_When_ChainNodeStorageOpens_Then_ItThrowsRatherThanSilentlyDroppingFreezerHistory()
        {
            var dataDir = NewDataDir("freezer-no-signer");
            var config = RocksDbConfig(dataDir);
            config.Storage.UseFreezerHistory = true;
            config.Storage.FreezerHistoryDirectory = Path.Combine(dataDir, "freezer-archive");

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using var storage = ChainNodeStorage.Open(config);
            });
        }

        [Fact]
        public async Task Given_ANonSplitNonFreezerConfig_When_ChainNodeStorageOpens_Then_TheManagerIsTheSameInstanceTheBundleWriteseThrough()
        {
            var dataDir = NewDataDir("appchain-shape");
            var config = RocksDbConfig(dataDir);

            await using var storage = ChainNodeStorage.Open(config);

            var bundle = Assert.IsType<RocksDbChainStoreBundle>(storage.Bundle);
            Assert.Same(bundle.Rocks, storage.Manager);
        }
    }
}
