using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RewindCoordinatorRocksDbTests : IDisposable
    {
        private readonly string _dataDir;

        public RewindCoordinatorRocksDbTests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"rewind_coord_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private static byte[] FillBytes(byte v) => Enumerable.Repeat(v, 32).ToArray();

        [Fact]
        public async Task RewindToAsync_JournalDisabledWithSnapshot_FallsBackToSnapshot()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(50, FillBytes(0x55), FillBytes(0x66));
            bundle.Metadata.Commit(100, FillBytes(0x64));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(60, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.SnapshotUsed, result.Outcome);
            Assert.NotNull(result.RestoredCheckpoint);
            Assert.Equal(50UL, result.RestoredCheckpoint!.Value.BlockNumber);
            Assert.Equal(50UL, result.NewHead);
            Assert.Equal(100UL, bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task RewindToAsync_SnapshotOnlyPolicy_SkipsJournalEvenWhenEnabled()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            await bundle.SaveCheckpointAsync(50, FillBytes(0x55), FillBytes(0x66));
            bundle.Metadata.Commit(100, FillBytes(0x64));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(75, RewindPolicy.SnapshotOnly);

            Assert.Equal(RewindOutcome.SnapshotUsed, result.Outcome);
            Assert.Equal(50UL, result.NewHead);
        }
    }
}
