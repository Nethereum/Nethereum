using System;
using System.Diagnostics;
using System.IO;
using Nethereum.DevChain.Storage.Sqlite;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class AutoTempDirectoriesDoNotAccumulateTests
    {
        private static string AutoTempRoot =>
            Path.Combine(Path.GetTempPath(), "nethereum-devchain");

        private static string CreateDirectoryStamped(string ownerStamp)
        {
            Directory.CreateDirectory(AutoTempRoot);
            var directory = Path.Combine(AutoTempRoot, $"{ownerStamp}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "chain.db"), "not a real database");
            return directory;
        }

        private static string StampOfThisProcess()
        {
            using var self = Process.GetCurrentProcess();
            return $"{self.Id}_{self.StartTime.Ticks}";
        }

        private static string StampOfAProcessThatHasExited()
        {
            using var exited = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            var stamp = $"{exited.Id}_{exited.StartTime.Ticks}";
            exited.WaitForExit();
            return stamp;
        }

        [Fact]
        public void Given_ADirectoryLeftByAProcessThatHasExited_When_TheSweepRuns_Then_ItIsRemoved()
        {
            var abandoned = CreateDirectoryStamped(StampOfAProcessThatHasExited());

            SqliteStorageManager.PurgeAbandonedAutoTempDirs();

            Assert.False(Directory.Exists(abandoned),
                "the process that owned this directory has exited, so nothing will ever clean it up but the sweep");
        }

        [Fact]
        public void Given_ADirectoryOwnedByALiveProcess_When_TheSweepRuns_Then_ItIsLeftAlone()
        {
            var live = CreateDirectoryStamped(StampOfThisProcess());
            Directory.SetCreationTimeUtc(live, DateTime.UtcNow.AddDays(-1));

            try
            {
                SqliteStorageManager.PurgeAbandonedAutoTempDirs();

                Assert.True(Directory.Exists(live),
                    "this directory is owned by a running process and predates the sweeper, which is what a " +
                    "concurrent devchain looks like; deleting it would destroy a live run's database");
            }
            finally
            {
                if (Directory.Exists(live)) Directory.Delete(live, recursive: true);
            }
        }

        [Fact]
        public void Given_ADirectoryWhoseNameCarriesNoOwner_When_TheSweepRuns_Then_ItIsRemoved()
        {
            var unstamped = CreateDirectoryStamped(Guid.NewGuid().ToString("N"));

            SqliteStorageManager.PurgeAbandonedAutoTempDirs();

            Assert.False(Directory.Exists(unstamped),
                "a directory left by an older build names no owner and can only be a leftover");
        }
    }
}
