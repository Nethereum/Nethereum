using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class BootRecoveryGateTests : IDisposable
    {
        private readonly string _dir;
        private readonly BootRecoveryGate _gate = new BootRecoveryGate(NullLogger.Instance);

        public BootRecoveryGateTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "brg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
            foreach (var stray in new[] { RestoreMarker, RestoreMarker + ".invalid" })
                try { if (File.Exists(stray)) File.Delete(stray); } catch { }
        }

        private string RestoreMarker =>
            _dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "." + BootRecoveryGate.RestoreRequestFileName;
        private string CleanMarker => Path.Combine(_dir, BootRecoveryGate.CleanShutdownMarkerFileName);

        [Fact]
        public void ApplyPendingRestore_NoMarker_IsNoOp()
        {
            _gate.ApplyPendingRestore(_dir);
            Assert.False(File.Exists(RestoreMarker));
            Assert.Empty(Directory.GetFiles(_dir));
        }

        [Fact]
        public void ApplyPendingRestore_UnreadableMarker_RenamedAsideAndBootsOnExistingData()
        {
            File.WriteAllText(RestoreMarker, "not-a-block-number");

            _gate.ApplyPendingRestore(_dir);

            Assert.False(File.Exists(RestoreMarker));
            Assert.True(File.Exists(RestoreMarker + ".invalid"));
        }

        [Fact]
        public void ApplyPendingRestore_ValidBlockButNoSnapshot_RenamedAside()
        {
            File.WriteAllText(RestoreMarker, "12345");
            var snapshotDir = RocksDbCheckpointManager.ResolveCheckpointSnapshotPath(_dir, 12345);
            Assert.False(Directory.Exists(snapshotDir));

            _gate.ApplyPendingRestore(_dir);

            Assert.False(File.Exists(RestoreMarker));
            Assert.True(File.Exists(RestoreMarker + ".invalid"));
        }

        [Fact]
        public void RecordRestoreRequest_ThenApplyPendingRestore_RoundTripsTheBlockNumber()
        {
            var wrote = _gate.RecordRestoreRequest(_dir, 25_548_588);
            Assert.True(wrote);
            Assert.True(File.Exists(RestoreMarker));
            Assert.Equal("25548588", File.ReadAllText(RestoreMarker).Trim());

            _gate.ApplyPendingRestore(_dir);
            Assert.False(File.Exists(RestoreMarker));
            Assert.True(File.Exists(RestoreMarker + ".invalid"));
        }

        [Fact]
        public void ApplyPendingRestore_LegacyInDirMarker_IsHonoured()
        {
            var legacy = Path.Combine(_dir, BootRecoveryGate.RestoreRequestFileName);
            File.WriteAllText(legacy, "12345");

            _gate.ApplyPendingRestore(_dir);

            Assert.False(File.Exists(legacy));
            Assert.True(File.Exists(legacy + ".invalid"));
        }

        [Fact]
        public void RecordRestoreRequest_EmptyDataDir_ReturnsFalse()
        {
            Assert.False(_gate.RecordRestoreRequest("", 1));
        }

        [Fact]
        public void MarkCleanShutdown_StampsMarker()
        {
            Assert.False(File.Exists(CleanMarker));
            _gate.MarkCleanShutdown(_dir);
            Assert.True(File.Exists(CleanMarker));
        }

        [Fact]
        public void EnsureConsistentOrEscalate_EmptyDataDir_IsNoOp()
        {
            _gate.EnsureConsistentOrEscalate(bundle: null, dataDir: "");
        }

        [Fact]
        public void EnsureConsistentOrEscalate_CleanShutdownMarkerPresent_ConsumesMarkerAndSkipsIntegrityCheck()
        {
            using var bundle = OpenTempBundle(out var dbDir);

            _gate.MarkCleanShutdown(dbDir);
            Assert.True(File.Exists(Path.Combine(dbDir, BootRecoveryGate.CleanShutdownMarkerFileName)));

            _gate.EnsureConsistentOrEscalate(bundle, dbDir);

            Assert.False(File.Exists(Path.Combine(dbDir, BootRecoveryGate.CleanShutdownMarkerFileName)));
        }

        private RocksDbChainStoreBundle OpenTempBundle(out string dbDir)
        {
            dbDir = Path.Combine(_dir, "db");
            Directory.CreateDirectory(dbDir);
            return RocksDbChainStoreBundle.Open(
                dbDir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dbDir,
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = 128,
                    TrieNodeHistoryIndex = true,
                });
        }
    }
}
