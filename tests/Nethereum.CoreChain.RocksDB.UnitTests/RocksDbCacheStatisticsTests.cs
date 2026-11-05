using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbCacheStatisticsTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "rocks-cachestats-" + Guid.NewGuid().ToString("N"));

        public RocksDbCacheStatisticsTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private static byte[] Key(int i) => SHA256.HashData(BitConverter.GetBytes(i));

        private static byte[] Value(int i) => SHA256.HashData(BitConverter.GetBytes(~i)).Concat(BitConverter.GetBytes(i)).ToArray();

        [Fact]
        public void Given_StatisticsEnabled_When_StateIsRead_Then_ReadCacheStatsReportsHitsAndMisses()
        {
            const int keys = 20_000;
            var options = new RocksDbStorageOptions { DatabasePath = _dir };
            using (var seed = new RocksDbManager(options))
            {
                for (var i = 0; i < keys; i++)
                    seed.Put(RocksDbManager.CF_STATE_ACCOUNTS, Key(i), Value(i));
                seed.Flush();
            }

            options.EnableStatistics = true;
            using var manager = new RocksDbManager(options);

            var before = manager.ReadCacheStats();
            AssertNonNegative(before);

            for (var i = 0; i < keys; i++)
                Assert.Equal(Value(i), manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, Key(i)));
            var afterColdReads = manager.ReadCacheStats();
            AssertNonNegative(afterColdReads);
            Assert.True(afterColdReads.DataMiss > before.DataMiss);
            Assert.True(afterColdReads.UsageBytes > 0);

            for (var i = 0; i < keys; i++)
                manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, Key(i));
            var afterWarmReads = manager.ReadCacheStats();
            Assert.True(afterWarmReads.DataHit > afterColdReads.DataHit);
            Assert.True(afterWarmReads.BytesRead > afterColdReads.BytesRead);
        }

        [Fact]
        public void Given_StatisticsDisabled_When_CacheStatsAreRead_Then_TheCallExplainsStatisticsAreOff()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            Assert.Throws<InvalidOperationException>(() => manager.ReadCacheStats());
        }

        private static void AssertNonNegative(RocksDbCacheStats s)
        {
            Assert.True(s.IndexHit >= 0 && s.IndexMiss >= 0 && s.FilterHit >= 0 && s.FilterMiss >= 0);
            Assert.True(s.DataHit >= 0 && s.DataMiss >= 0 && s.BytesRead >= 0 && s.UsageBytes >= 0);
        }
    }
}
