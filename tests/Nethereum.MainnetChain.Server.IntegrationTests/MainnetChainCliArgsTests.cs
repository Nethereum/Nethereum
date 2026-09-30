using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class MainnetChainCliArgsTests
    {
        [Fact]
        public void FlushCadenceFlag_SetsFlushCadenceBlocks()
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--flush-cadence", "24" });
            Assert.Equal(24, config.FlushCadenceBlocks);
        }

        [Fact]
        public void CheckpointEveryFlag_SetsCheckpointEvery()
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--checkpoint-every", "1000" });
            Assert.Equal(1000UL, config.CheckpointEvery);
        }

        [Fact]
        public void CacheSizeFlag_SetsBlockCacheSize()
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--cache-size", "4G" });
            Assert.Equal(4294967296L, config.BlockCacheSize);
        }

        [Theory]
        [InlineData("512M", 536870912L)]
        [InlineData("1073741824", 1073741824L)]
        public void CacheSizeFlag_ParsesPlainAndSuffixedSizes(string input, long expectedBytes)
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--cache-size", input });
            Assert.Equal(expectedBytes, config.BlockCacheSize);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("")]
        [InlineData("abc")]
        public void CacheSizeFlag_RejectsInvalidSize_LeavesDefault(string input)
        {
            var config = new MainnetChainServerConfig();
            var defaultCache = config.BlockCacheSize;
            MainnetChainCliArgs.Apply(config, new[] { "--cache-size", input });
            Assert.Equal(defaultCache, config.BlockCacheSize);
        }

        [Fact]
        public void EnableTxSubmissionFlag_OffByDefault_OnWhenFlagged()
        {
            var config = new MainnetChainServerConfig();
            Assert.False(config.EnableTxSubmission);
            MainnetChainCliArgs.Apply(config, new[] { "--enable-tx-submission" });
            Assert.True(config.EnableTxSubmission);
        }

        [Fact]
        public void HistoryBackfillFlag_DefaultsToDuringStateSync()
        {
            var config = new MainnetChainServerConfig();
            Assert.Equal(HistoryBackfillMode.DuringStateSync, config.HistoryBackfill);
        }

        [Theory]
        [InlineData("during", HistoryBackfillMode.DuringStateSync)]
        [InlineData("after", HistoryBackfillMode.AfterStateSync)]
        [InlineData("off", HistoryBackfillMode.Never)]
        [InlineData("never", HistoryBackfillMode.Never)]
        [InlineData("AFTER", HistoryBackfillMode.AfterStateSync)]
        public void HistoryBackfillFlag_SetsMode(string value, HistoryBackfillMode expected)
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--history-backfill", value });
            Assert.Equal(expected, config.HistoryBackfill);
        }

        [Theory]
        [InlineData("")]
        [InlineData("bogus")]
        public void HistoryBackfillFlag_InvalidValue_LeavesDefault(string value)
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--history-backfill", value });
            Assert.Equal(HistoryBackfillMode.DuringStateSync, config.HistoryBackfill);
        }

        [Fact]
        public void RawConfigurationBinding_StillBindsAdvancedProperties()
        {
            var config = new MainnetChainServerConfig();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MainnetChain:FlushCadenceBlocks"] = "24",
                    ["MainnetChain:BlockCacheSize"] = "2048"
                })
                .Build();

            configuration.GetSection("MainnetChain").Bind(config);

            Assert.Equal(24, config.FlushCadenceBlocks);
            Assert.Equal(2048L, config.BlockCacheSize);
        }
    }
}
