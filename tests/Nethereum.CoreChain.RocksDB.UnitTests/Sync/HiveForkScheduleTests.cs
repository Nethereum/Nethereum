using System;
using System.IO;
using System.Linq;
using Nethereum.EVM;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class HiveForkScheduleTests
    {
        private static JObject ForkEnv() => JObject.Parse(
            File.ReadAllText(Path.Combine(HiveTestdataFixture.TestdataDir, HiveForkSchedule.ForkEnvFileName)));

        private static ulong Timestamp(string key) => ulong.Parse(ForkEnv()[key].ToString());

        [Fact]
        public void ResolveAt_MatchesForkEnvTimestamps()
        {
            var activations = HiveTestdataFixture.ChainActivations;
            var cancun = Timestamp("HIVE_CANCUN_TIMESTAMP");
            var prague = Timestamp("HIVE_PRAGUE_TIMESTAMP");

            Assert.Equal(HardforkName.Shanghai, activations.ResolveAt(0, Timestamp("HIVE_SHANGHAI_TIMESTAMP")));
            Assert.Equal(HardforkName.Shanghai, activations.ResolveAt(0, cancun - 1));
            Assert.Equal(HardforkName.Cancun, activations.ResolveAt(0, cancun));
            Assert.Equal(HardforkName.Cancun, activations.ResolveAt(0, prague - 1));
            Assert.Equal(HardforkName.Prague, activations.ResolveAt(0, prague));
        }

        [Fact]
        public void Chain_CrossesEveryScheduledFork()
        {
            var activations = HiveTestdataFixture.ChainActivations;
            var resolved = HiveTestdataFixture.Chain
                .Select(b => activations.ResolveAt((long)b.Header.BlockNumber, (ulong)b.Header.Timestamp))
                .ToHashSet();

            var scheduled = ForkEnv().Properties()
                .Select(p => p.Name)
                .Count(n => n.EndsWith("_TIMESTAMP", StringComparison.Ordinal));

            Assert.Equal(scheduled, resolved.Count);
            Assert.Contains(HardforkName.Prague, resolved);
        }

        [Fact]
        public void UnknownTimestampFork_FailsNamingIt()
        {
            var forkEnv = ForkEnv();
            forkEnv["HIVE_BPO1_TIMESTAMP"] = "180";

            var ex = Assert.Throws<InvalidOperationException>(
                () => HiveForkSchedule.FromForkEnv(forkEnv, HiveTestdataFixture.HardforkConfigFactory));
            Assert.Contains("HIVE_BPO1_TIMESTAMP", ex.Message);
        }

        [Fact]
        public void PreMergeForkActivatedAfterGenesis_FailsNamingIt()
        {
            var forkEnv = ForkEnv();
            forkEnv["HIVE_FORK_BERLIN"] = "48";

            var ex = Assert.Throws<InvalidOperationException>(
                () => HiveForkSchedule.FromForkEnv(forkEnv, HiveTestdataFixture.HardforkConfigFactory));
            Assert.Contains("HIVE_FORK_BERLIN=48", ex.Message);
        }

        [Fact]
        public void BlobLimitDisagreeingWithOurForkConfig_Fails()
        {
            var forkEnv = ForkEnv();
            forkEnv["HIVE_PRAGUE_BLOB_MAX"] = "12";

            var ex = Assert.Throws<InvalidOperationException>(
                () => HiveForkSchedule.FromForkEnv(forkEnv, HiveTestdataFixture.HardforkConfigFactory));
            Assert.Contains("HIVE_PRAGUE_BLOB_MAX", ex.Message);
        }
    }
}
