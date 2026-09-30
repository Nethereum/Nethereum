using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.RocksDB;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class StorageOptionsCompositionTests
    {
        [Fact]
        public void Default_Config_Maps_To_PathKeyed_Pruned_ProofServing()
        {
            var config = new MainnetChainServerConfig { DataDir = "./x" };

            var opts = MainnetNodeComposition.BuildStorageOptions(config);

            Assert.True(opts.PathKeyedState);
            Assert.Equal(128, opts.TrieNodeHistoryBlocks);
            Assert.True(opts.TrieNodeHistoryIndex);
            opts.Validate();
        }

        [Fact]
        public void HashKeyed_Override_Maps_To_Legacy_Options_And_Validates()
        {
            var config = new MainnetChainServerConfig
            {
                DataDir = "./x",
                PathKeyedState = false,
                TrieNodeHistoryBlocks = -1,
                TrieNodeHistoryIndex = false,
            };

            var opts = MainnetNodeComposition.BuildStorageOptions(config);

            Assert.False(opts.PathKeyedState);
            Assert.Equal(-1, opts.TrieNodeHistoryBlocks);
            Assert.False(opts.TrieNodeHistoryIndex);
            opts.Validate();
        }

        [Fact]
        public void PathKeyed_Archive_Config_Maps_And_Validates()
        {
            var config = new MainnetChainServerConfig
            {
                DataDir = "./x",
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 0,
                TrieNodeHistoryIndex = true,
            };

            var opts = MainnetNodeComposition.BuildStorageOptions(config);

            Assert.True(opts.PathKeyedState);
            Assert.Equal(0, opts.TrieNodeHistoryBlocks);
            Assert.True(opts.TrieNodeHistoryIndex);
            opts.Validate();
        }

        [Fact]
        public void Given_ConfigUseFreezerHistory_When_BuildStorageOptions_Then_MappedThrough()
        {
            var config = new MainnetChainServerConfig
            {
                DataDir = "./x",
                UseFreezerHistory = true,
                FreezerHistoryDirectory = "./x/freezer",
            };

            var opts = MainnetNodeComposition.BuildStorageOptions(config);

            Assert.True(opts.UseFreezerHistory);
            Assert.Equal("./x/freezer", opts.FreezerHistoryDirectory);
        }

        [Fact]
        public void Given_AMainnetConfigWithEveryStorageFieldSet_When_MappedThroughChainNodeConfig_Then_BuildStorageOptionsMatchesMainnetsFieldForField()
        {
            var config = new MainnetChainServerConfig
            {
                DataDir = "./x",
                PathKeyedState = false,
                TrieNodeHistoryBlocks = 256,
                TrieNodeHistoryIndex = true,
                BlockCacheSize = 4096,
                SplitHistoryStore = true,
                HotWindowBlocks = 512,
                EnableLogIndex = true,
                PromotionEnabled = true,
                UseFreezerHistory = true,
                FreezerHistoryDirectory = "./x/freezer",
                BackgroundFreezeIndexing = true,
                FreezerBackgroundDegreeOfParallelism = 7,
            };

            var mainnetOptions = MainnetNodeComposition.BuildStorageOptions(config);
            var sharedHostOptions = ChainNodeStorage.BuildStorageOptions(config.ToChainNodeConfig().Storage);

            foreach (var property in typeof(RocksDbStorageOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == nameof(RocksDbStorageOptions.DatabasePath)) continue;

                var mainnetValue = property.GetValue(mainnetOptions);
                var sharedHostValue = property.GetValue(sharedHostOptions);
                Assert.True(
                    Equals(mainnetValue, sharedHostValue),
                    $"{property.Name}: mainnet={mainnetValue}, shared-host={sharedHostValue}");
            }
        }

        [Fact]
        public async Task Given_AMainnetFreezerBackgroundDegreeOfParallelism_When_OpenedThroughTheSharedHost_Then_ItReachesTheOpenedRocksDbManager()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), $"mainnet-parallelism_{Guid.NewGuid():N}");
            try
            {
                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    FreezerBackgroundDegreeOfParallelism = 7,
                };

                await using var storage = ChainNodeStorage.Open(
                    config.ToChainNodeConfig(), signer: new TransactionVerificationAndRecoveryImp());

                Assert.Equal(7, storage.Manager.Options.FreezerBackgroundDegreeOfParallelism);
            }
            finally
            {
                if (Directory.Exists(dataDir)) { try { Directory.Delete(dataDir, true); } catch { } }
            }
        }

        [Fact]
        public async Task Given_MainnetProductionSplitHistoryStore_When_OpenedThroughTheSharedHost_Then_ItOpensTheSplitBundleWithoutThrowing()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), $"mainnet-split_{Guid.NewGuid():N}");
            try
            {
                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    SplitHistoryStore = true,
                };

                await using var storage = ChainNodeStorage.Open(
                    config.ToChainNodeConfig(), signer: new TransactionVerificationAndRecoveryImp());

                var bundle = Assert.IsType<RocksDbChainStoreBundle>(storage.Bundle);
                var coreDir = Path.Combine(dataDir, RocksDbChainStoreBundle.CoreSubDir);
                var historyDir = Path.Combine(dataDir, RocksDbChainStoreBundle.HistorySubDir);
                Assert.True(File.Exists(Path.Combine(coreDir, "CURRENT")));
                Assert.True(File.Exists(Path.Combine(historyDir, "CURRENT")));
            }
            finally
            {
                if (Directory.Exists(dataDir)) { try { Directory.Delete(dataDir, true); } catch { } }
            }
        }

        [Fact]
        public async Task Given_MainnetProductionUseFreezerHistory_When_OpenedThroughTheSharedHost_Then_ItOpensTheFreezerBundleWithoutThrowing()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), $"mainnet-freezer_{Guid.NewGuid():N}");
            try
            {
                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    UseFreezerHistory = true,
                    FreezerHistoryDirectory = Path.Combine(dataDir, "freezer-archive"),
                };

                await using var storage = ChainNodeStorage.Open(
                    config.ToChainNodeConfig(), signer: new TransactionVerificationAndRecoveryImp());

                var bundle = Assert.IsType<RocksDbChainStoreBundle>(storage.Bundle);
                Assert.Equal(0, bundle.FreezerHead);
            }
            finally
            {
                if (Directory.Exists(dataDir)) { try { Directory.Delete(dataDir, true); } catch { } }
            }
        }
    }
}
