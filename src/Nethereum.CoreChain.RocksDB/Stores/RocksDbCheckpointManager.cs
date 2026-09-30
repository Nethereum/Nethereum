using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbCheckpointManager
    {
        internal const string CheckpointArchiveDirName = ".cp";
        private const string CheckpointDirFormat = "D12";

        private static readonly string[] DefaultPreserveSubdirs =
            { CheckpointArchiveDirName, RocksDbChainStoreBundle.FreezerHistorySubDir };

        private const string SnapshotIdentityFileName = "nethereum-checkpoint.id";

        private readonly RocksDbManager _rocks;
        private readonly string _dataDir;
        private readonly string _archiveDir;
        private readonly IChainMetadataStore _metadata;
        private readonly RocksDbManager _historyRocks;
        private readonly string _historyDataDir;
        private readonly string _historyArchiveDir;
        private readonly RocksDbManager _freezerHistoryRocks;
        private readonly string _freezerHistoryDataDir;
        private readonly string _freezerHistoryArchiveDir;
        private readonly string _freezerDirectory;
        private readonly object _freezerAppendLock;
        private readonly Func<long> _freezerItemsReader;

        public RocksDbCheckpointManager(RocksDbManager rocks, string dataDir, IChainMetadataStore metadata)
        {
            _rocks = rocks;
            _dataDir = dataDir;
            _archiveDir = Path.Combine(dataDir, CheckpointArchiveDirName);
            _metadata = metadata;
        }

        public RocksDbCheckpointManager(
            RocksDbManager rocks, string dataDir, IChainMetadataStore metadata,
            RocksDbManager historyRocks, string historyDataDir)
            : this(rocks, dataDir, metadata)
        {
            _historyRocks = historyRocks ?? throw new ArgumentNullException(nameof(historyRocks));
            _historyDataDir = historyDataDir ?? throw new ArgumentNullException(nameof(historyDataDir));
            _historyArchiveDir = Path.Combine(historyDataDir, CheckpointArchiveDirName);
        }

        public RocksDbCheckpointManager(
            RocksDbManager rocks, string dataDir, IChainMetadataStore metadata,
            RocksDbManager freezerHistoryRocks, string freezerHistoryDataDir,
            string freezerDirectory, object freezerAppendLock, Func<long> freezerItemsReader)
            : this(rocks, dataDir, metadata)
        {
            _freezerHistoryRocks = freezerHistoryRocks ?? throw new ArgumentNullException(nameof(freezerHistoryRocks));
            _freezerHistoryDataDir = freezerHistoryDataDir ?? throw new ArgumentNullException(nameof(freezerHistoryDataDir));
            _freezerHistoryArchiveDir = Path.Combine(freezerHistoryDataDir, CheckpointArchiveDirName);
            if (string.IsNullOrWhiteSpace(freezerDirectory)) throw new ArgumentException("freezerDirectory required", nameof(freezerDirectory));
            _freezerDirectory = freezerDirectory;
            _freezerAppendLock = freezerAppendLock ?? throw new ArgumentNullException(nameof(freezerAppendLock));
            _freezerItemsReader = freezerItemsReader ?? throw new ArgumentNullException(nameof(freezerItemsReader));
        }

        public RocksDbCheckpointManager(
            RocksDbManager rocks, string dataDir, IChainMetadataStore metadata,
            RocksDbManager historyRocks, string historyDataDir,
            RocksDbManager freezerHistoryRocks, string freezerHistoryDataDir,
            string freezerDirectory, object freezerAppendLock, Func<long> freezerItemsReader)
            : this(rocks, dataDir, metadata, historyRocks, historyDataDir)
        {
            _freezerHistoryRocks = freezerHistoryRocks ?? throw new ArgumentNullException(nameof(freezerHistoryRocks));
            _freezerHistoryDataDir = freezerHistoryDataDir ?? throw new ArgumentNullException(nameof(freezerHistoryDataDir));
            _freezerHistoryArchiveDir = Path.Combine(freezerHistoryDataDir, CheckpointArchiveDirName);
            if (string.IsNullOrWhiteSpace(freezerDirectory)) throw new ArgumentException("freezerDirectory required", nameof(freezerDirectory));
            _freezerDirectory = freezerDirectory;
            _freezerAppendLock = freezerAppendLock ?? throw new ArgumentNullException(nameof(freezerAppendLock));
            _freezerItemsReader = freezerItemsReader ?? throw new ArgumentNullException(nameof(freezerItemsReader));
        }

        private bool IsSplit => _historyRocks != null;
        private bool IsFreezer => _freezerHistoryRocks != null;

        public string ResolveCheckpointSnapshotPath(ulong blockNumber)
            => Path.Combine(_archiveDir, blockNumber.ToString(CheckpointDirFormat));

        public static string ResolveCheckpointSnapshotPath(string dataDir, ulong blockNumber)
            => Path.Combine(dataDir, CheckpointArchiveDirName, blockNumber.ToString(CheckpointDirFormat));

        public async Task<ChainCheckpoint> SaveCheckpointAsync(
            ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
        {
            if (stateRoot is null || stateRoot.Length == 0) throw new ArgumentException("stateRoot required", nameof(stateRoot));
            if (blockHash is null || blockHash.Length == 0) throw new ArgumentException("blockHash required", nameof(blockHash));

            if (IsFreezer)
            {
                SaveFreezerCheckpoint(blockNumber, stateRoot, blockHash);
            }
            else if (IsSplit)
            {
                var historySaved = SaveOneCheckpoint(_historyRocks, _historyArchiveDir, blockNumber, stateRoot, blockHash);
                if (historySaved) ct.ThrowIfCancellationRequested();
                SaveOneCheckpoint(_rocks, _archiveDir, blockNumber, stateRoot, blockHash);
            }
            else
            {
                SaveOneCheckpoint(_rocks, _archiveDir, blockNumber, stateRoot, blockHash);
            }

            try
            {
                _metadata.SaveCheckpoint(blockNumber, stateRoot, blockHash);
            }
            catch
            {
                try { Directory.Delete(ResolveCheckpointSnapshotPath(blockNumber), recursive: true); } catch { }
                if (IsSplit) { try { Directory.Delete(ResolveHistoryCheckpointSnapshotPath(blockNumber), recursive: true); } catch { } }
                if (IsFreezer) { try { Directory.Delete(ResolveFreezerHistoryCheckpointSnapshotPath(blockNumber), recursive: true); } catch { } }
                throw;
            }

            await Task.CompletedTask;
            return _metadata.GetCheckpoint(blockNumber)
                   ?? throw new InvalidOperationException(
                       $"_metadata.SaveCheckpoint at {blockNumber} returned but GetCheckpoint reads back null.");
        }

        private static bool SaveOneCheckpoint(
            RocksDbManager rocks, string archiveDir, ulong blockNumber, byte[] stateRoot, byte[] blockHash,
            Action<string> extraMarkerWriter = null)
        {
            var snapshotDir = Path.Combine(archiveDir, blockNumber.ToString(CheckpointDirFormat));
            var snapshotStaging = snapshotDir + ".staging." + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (Directory.Exists(snapshotDir))
            {
                if (SnapshotIdentityMatches(snapshotDir, stateRoot, blockHash))
                    return false;

                try { Directory.Delete(snapshotDir, recursive: true); } catch { }
            }

            Directory.CreateDirectory(archiveDir);

            try
            {
                rocks.CreateDatabaseCheckpoint(snapshotStaging);
                WriteSnapshotIdentity(snapshotStaging, stateRoot, blockHash);
                extraMarkerWriter?.Invoke(snapshotStaging);
                Directory.Move(snapshotStaging, snapshotDir);
                return true;
            }
            catch
            {
                if (Directory.Exists(snapshotStaging))
                {
                    try { Directory.Delete(snapshotStaging, recursive: true); } catch { }
                }
                throw;
            }
        }

        public string ResolveHistoryCheckpointSnapshotPath(ulong blockNumber)
            => IsSplit ? Path.Combine(_historyArchiveDir, blockNumber.ToString(CheckpointDirFormat)) : null;

        public string ResolveFreezerHistoryCheckpointSnapshotPath(ulong blockNumber)
            => IsFreezer ? Path.Combine(_freezerHistoryArchiveDir, blockNumber.ToString(CheckpointDirFormat)) : null;

        private void SaveFreezerCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash)
        {
            lock (_freezerAppendLock)
            {
                var freezerItems = _freezerItemsReader();
                SaveOneCheckpoint(_freezerHistoryRocks, _freezerHistoryArchiveDir, blockNumber, stateRoot, blockHash,
                    snapshotStaging => WriteFreezerItemsMarker(snapshotStaging, freezerItems));
                if (IsSplit)
                    SaveOneCheckpoint(_historyRocks, _historyArchiveDir, blockNumber, stateRoot, blockHash);
                SaveOneCheckpoint(_rocks, _archiveDir, blockNumber, stateRoot, blockHash);
            }
        }

        private const string FreezerItemsMarkerFileName = "nethereum-freezer-items.marker";

        private static void WriteFreezerItemsMarker(string snapshotDir, long freezerItems)
            => File.WriteAllText(
                Path.Combine(snapshotDir, FreezerItemsMarkerFileName),
                freezerItems.ToString(System.Globalization.CultureInfo.InvariantCulture));

        private static long ReadFreezerItemsMarker(string snapshotDir)
        {
            var markerPath = Path.Combine(snapshotDir, FreezerItemsMarkerFileName);
            if (!File.Exists(markerPath))
                throw new InvalidOperationException(
                    $"Freezer checkpoint snapshot at {snapshotDir} is missing its {FreezerItemsMarkerFileName} marker " +
                    "(a checkpoint saved before Task 7, or a corrupted/incomplete snapshot). Refusing to restore.");
            var raw = File.ReadAllText(markerPath).Trim();
            if (!long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var items))
                throw new InvalidOperationException(
                    $"Freezer checkpoint snapshot at {snapshotDir} has a corrupt {FreezerItemsMarkerFileName} marker: '{raw}'.");
            return items;
        }

        private static void WriteSnapshotIdentity(string snapshotDir, byte[] stateRoot, byte[] blockHash)
        {
            using var ms = new MemoryStream();
            void WriteField(byte[] value)
            {
                var len = BitConverter.GetBytes(value.Length);
                ms.Write(len, 0, len.Length);
                ms.Write(value, 0, value.Length);
            }
            WriteField(stateRoot);
            WriteField(blockHash);
            File.WriteAllBytes(Path.Combine(snapshotDir, SnapshotIdentityFileName), ms.ToArray());
        }

        private static bool SnapshotIdentityMatches(string snapshotDir, byte[] stateRoot, byte[] blockHash)
        {
            try
            {
                var markerPath = Path.Combine(snapshotDir, SnapshotIdentityFileName);
                if (!File.Exists(markerPath)) return false;
                var raw = File.ReadAllBytes(markerPath);

                int offset = 0;
                byte[] ReadField()
                {
                    if (offset + 4 > raw.Length) return null;
                    int len = BitConverter.ToInt32(raw, offset);
                    offset += 4;
                    if (len < 0 || len > raw.Length - offset) return null;
                    var field = new byte[len];
                    Array.Copy(raw, offset, field, 0, len);
                    offset += len;
                    return field;
                }

                var storedStateRoot = ReadField();
                var storedBlockHash = ReadField();
                return storedStateRoot != null && storedBlockHash != null
                    && ByteUtil.AreEqual(storedStateRoot, stateRoot)
                    && ByteUtil.AreEqual(storedBlockHash, blockHash);
            }
            catch { return false; }
        }

        public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
        {
            var rows = _metadata.ListCheckpointBlockNumbers();
            var result = new List<ChainCheckpoint>(rows.Count);
            foreach (var bn in rows)
            {
                var cp = _metadata.GetCheckpoint(bn);
                if (cp is null) continue;
                if (!Directory.Exists(ResolveCheckpointSnapshotPath(bn))) continue;
                if (IsSplit && !Directory.Exists(ResolveHistoryCheckpointSnapshotPath(bn))) continue;
                if (IsFreezer && !Directory.Exists(ResolveFreezerHistoryCheckpointSnapshotPath(bn))) continue;
                result.Add(cp.Value);
            }
            return Task.FromResult<IReadOnlyList<ChainCheckpoint>>(result);
        }

        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            _metadata.DeleteCheckpoint(blockNumber);
            var snapshotDir = ResolveCheckpointSnapshotPath(blockNumber);
            if (Directory.Exists(snapshotDir))
            {
                try { Directory.Delete(snapshotDir, recursive: true); } catch { }
            }
            if (IsSplit)
            {
                var historySnapshotDir = ResolveHistoryCheckpointSnapshotPath(blockNumber);
                if (Directory.Exists(historySnapshotDir))
                {
                    try { Directory.Delete(historySnapshotDir, recursive: true); } catch { }
                }
            }
            if (IsFreezer)
            {
                var freezerHistorySnapshotDir = ResolveFreezerHistoryCheckpointSnapshotPath(blockNumber);
                if (Directory.Exists(freezerHistorySnapshotDir))
                {
                    try { Directory.Delete(freezerHistorySnapshotDir, recursive: true); } catch { }
                }
            }
            return Task.CompletedTask;
        }

        public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            var snapshotDir = ResolveCheckpointSnapshotPath(blockNumber);
            if (!Directory.Exists(snapshotDir))
                throw new InvalidOperationException(
                    $"No snapshot at {snapshotDir} for block {blockNumber}. Use ListCheckpointsAsync to enumerate usable checkpoints.");

            if (IsFreezer)
            {
                var freezerHistorySnapshotDir = ResolveFreezerHistoryCheckpointSnapshotPath(blockNumber);
                if (!Directory.Exists(freezerHistorySnapshotDir))
                    throw new InvalidOperationException(
                        $"No FREEZER-HISTORY-side snapshot at {freezerHistorySnapshotDir} for block {blockNumber} " +
                        $"(core-side snapshot exists at {snapshotDir}) — an incomplete/one-sided freezer checkpoint. " +
                        "Refusing a core-only restore.");

                if (IsSplit)
                {
                    var freezerItems = ReadFreezerItemsMarker(freezerHistorySnapshotDir);

                    RestoreFromCheckpointDir(freezerHistorySnapshotDir, _freezerHistoryDataDir, scope: CatalogueScope.FreezerHistory);

                    var historySnapshotDir = ResolveHistoryCheckpointSnapshotPath(blockNumber);
                    if (!Directory.Exists(historySnapshotDir))
                        throw new InvalidOperationException(
                            $"No HISTORY-side snapshot at {historySnapshotDir} for block {blockNumber} (core-side " +
                            $"snapshot exists at {snapshotDir}) — an incomplete/one-sided split+freezer checkpoint. " +
                            "Refusing a core-only restore.");
                    RestoreFromCheckpointDir(historySnapshotDir, _historyDataDir, scope: CatalogueScope.History);
                    RestoreFromCheckpointDir(snapshotDir, _dataDir, scope: CatalogueScope.Core);
                    StorePairingGuard.ClearPairing(_dataDir, _historyDataDir);

                    RestoreFreezerFilesTo(freezerItems);
                }
                else
                {
                    RestoreFromCheckpointDir(snapshotDir, _dataDir,
                        promotionEnabled: _rocks.Options.PromotionEnabled, freezerDirectory: _freezerDirectory);
                }
                return Task.CompletedTask;
            }

            if (IsSplit)
            {
                var historySnapshotDir = ResolveHistoryCheckpointSnapshotPath(blockNumber);
                if (!Directory.Exists(historySnapshotDir))
                    throw new InvalidOperationException(
                        $"No HISTORY-side snapshot at {historySnapshotDir} for block {blockNumber} (core-side snapshot " +
                        $"exists at {snapshotDir}) — an incomplete/one-sided split checkpoint. Refusing a core-only restore.");

                RestoreFromCheckpointDir(historySnapshotDir, _historyDataDir, scope: CatalogueScope.History);
                RestoreFromCheckpointDir(snapshotDir, _dataDir, scope: CatalogueScope.Core);

                StorePairingGuard.ClearPairing(_dataDir, _historyDataDir);
                return Task.CompletedTask;
            }

            RestoreFromCheckpointDir(snapshotDir, _dataDir);
            return Task.CompletedTask;
        }

        private void RestoreFreezerFilesTo(long freezerItems) => TruncateFreezerArchiveTo(_freezerDirectory, freezerItems);

        private static void TruncateFreezerArchiveTo(string freezerDirectory, long freezerItems)
        {
            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDirectory), Nethereum.Freezer.FreezerOpenMode.Append);

            if (freezer.Items < freezerItems)
                throw new InvalidOperationException(
                    $"Cannot restore freezer checkpoint recorded at {freezerItems} items: the live archive at " +
                    $"'{freezerDirectory}' only holds {freezer.Items} — the archive is BEHIND the checkpoint, so " +
                    "the restored freezer-history index would reference frozen blocks that no longer exist. Restore " +
                    "a checkpoint the archive was not rewound past, or rebuild the archive first.");

            freezer.TruncateHead(freezerItems);
        }

        private static string RestoreNewDir(string targetDir) => TrimDirSeparators(targetDir) + ".restore-new";
        private static string RestoreOldDir(string targetDir) => TrimDirSeparators(targetDir) + ".restore-old";

        private const string RestoreReadyMarkerName = ".restore-ready";
        private const string RestoreReadyMarkerContent = "nethereum-checkpoint-restore-v1";

        private static string TrimDirSeparators(string p) =>
            p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public static void RestoreFromCheckpointDir(
            string snapshotDir,
            string targetDir,
            IEnumerable<string> preserveSubdirs = null,
            CatalogueScope scope = CatalogueScope.Both,
            bool promotionEnabled = false,
            string freezerDirectory = null)
        {
            if (string.IsNullOrEmpty(snapshotDir)) throw new ArgumentException("snapshotDir required", nameof(snapshotDir));
            if (string.IsNullOrEmpty(targetDir)) throw new ArgumentException("targetDir required", nameof(targetDir));
            snapshotDir = Path.GetFullPath(snapshotDir);
            targetDir = Path.GetFullPath(targetDir);
            if (!Directory.Exists(snapshotDir))
                throw new InvalidOperationException($"Snapshot dir not found: {snapshotDir}");

            var freezerHistorySnapshotDir = Path.Combine(
                targetDir, RocksDbChainStoreBundle.FreezerHistorySubDir, CheckpointArchiveDirName, Path.GetFileName(snapshotDir));
            var isFreezerCheckpoint = !string.IsNullOrWhiteSpace(freezerDirectory) && Directory.Exists(freezerHistorySnapshotDir);
            long freezerItemsTarget = 0;

            if (isFreezerCheckpoint)
            {
                freezerItemsTarget = ReadFreezerItemsMarker(freezerHistorySnapshotDir);
                RequireFreezerArchiveCanReach(freezerDirectory, freezerItemsTarget);

                RestoreFromCheckpointDir(freezerHistorySnapshotDir,
                    Path.Combine(targetDir, RocksDbChainStoreBundle.FreezerHistorySubDir),
                    scope: CatalogueScope.FreezerHistory);
            }

            RestoreCoreDbFromCheckpointDir(snapshotDir, targetDir, preserveSubdirs, scope, promotionEnabled);

            if (isFreezerCheckpoint)
            {
                TruncateFreezerArchiveTo(freezerDirectory, freezerItemsTarget);
            }
        }

        private static void RequireFreezerArchiveCanReach(string freezerDirectory, long freezerItems)
        {
            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDirectory), Nethereum.Freezer.FreezerOpenMode.ReadOnly);

            if (freezer.Items < freezerItems)
                throw new InvalidOperationException(
                    $"Cannot restore freezer checkpoint recorded at {freezerItems} items: the live archive at " +
                    $"'{freezerDirectory}' only holds {freezer.Items} — the archive is BEHIND the checkpoint, so " +
                    "the restore would leave the freezer-history index referencing frozen blocks that no longer " +
                    "exist. Refusing before any RocksDB side is touched. Restore a checkpoint the archive was " +
                    "not rewound past, or rebuild the archive first.");
        }

        private static void RestoreCoreDbFromCheckpointDir(
            string snapshotDir, string targetDir, IEnumerable<string> preserveSubdirs, CatalogueScope scope, bool promotionEnabled)
        {
            var preserveSet = new HashSet<string>(
                preserveSubdirs ?? DefaultPreserveSubdirs,
                StringComparer.OrdinalIgnoreCase);

            CompleteInterruptedRestore(targetDir, preserveSet);
            if (!Directory.Exists(snapshotDir))
                throw new InvalidOperationException($"Snapshot dir not found after baseline recovery: {snapshotDir}");

            ProbeStagingSharesVolumeOrThrow(targetDir);

            var newDir = RestoreNewDir(targetDir);

            if (Directory.Exists(newDir)) Directory.Delete(newDir, recursive: true);
            using (var temp = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = snapshotDir, PromotionEnabled = promotionEnabled }, scope))
            {
                temp.CreateDatabaseCheckpoint(newDir);
            }
            File.WriteAllText(Path.Combine(newDir, RestoreReadyMarkerName), RestoreReadyMarkerContent);

            InstallStagedReplacement(targetDir, newDir, RestoreOldDir(targetDir),
                Path.Combine(targetDir, RestoreReadyMarkerName), preserveSet);
        }

        private static void InstallStagedReplacement(
            string targetDir, string newDir, string oldDir, string readyInTarget, HashSet<string> preserveSet)
        {
            if (Directory.Exists(targetDir))
            {
                if (Directory.Exists(oldDir)) Directory.Delete(oldDir, recursive: true);
                Directory.Move(targetDir, oldDir);
            }
            Directory.Move(newDir, targetDir);
            FoldPreservedSubdirs(oldDir, targetDir, preserveSet);
            if (Directory.Exists(oldDir)) Directory.Delete(oldDir, recursive: true);
            TryDeleteFile(readyInTarget);
        }

        public static void CompleteInterruptedRestore(string targetDir, IEnumerable<string> preserveSubdirs = null)
        {
            if (string.IsNullOrEmpty(targetDir)) return;
            targetDir = Path.GetFullPath(targetDir);
            var preserveSet = new HashSet<string>(
                preserveSubdirs ?? DefaultPreserveSubdirs,
                StringComparer.OrdinalIgnoreCase);

            var newDir = RestoreNewDir(targetDir);
            var oldDir = RestoreOldDir(targetDir);
            var readyInNew = Path.Combine(newDir, RestoreReadyMarkerName);
            var readyInTarget = Path.Combine(targetDir, RestoreReadyMarkerName);

            if (Directory.Exists(newDir) && IsReadyMarker(readyInNew))
            {
                try
                {
                    InstallStagedReplacement(targetDir, newDir, oldDir, readyInTarget, preserveSet);
                }
                catch
                {
                    if (!Directory.Exists(targetDir) && Directory.Exists(oldDir))
                    {
                        try { Directory.Move(oldDir, targetDir); } catch { }
                    }
                    if (Directory.Exists(newDir)) { try { Directory.Delete(newDir, recursive: true); } catch { } }
                }
                return;
            }
            if (Directory.Exists(newDir))
            {
                try { Directory.Delete(newDir, recursive: true); } catch { }
            }

            if (IsReadyMarker(readyInTarget))
            {
                FoldPreservedSubdirs(oldDir, targetDir, preserveSet);
                if (Directory.Exists(oldDir)) { try { Directory.Delete(oldDir, recursive: true); } catch { } }
                TryDeleteFile(readyInTarget);
            }
            else if (Directory.Exists(oldDir))
            {
                if (Directory.Exists(targetDir))
                {
                    FoldPreservedSubdirs(oldDir, targetDir, preserveSet);
                    try { Directory.Delete(oldDir, recursive: true); } catch { }
                }
                else
                {
                    Directory.Move(oldDir, targetDir);
                }
            }
        }

        private static void FoldPreservedSubdirs(string srcDir, string dstDir, HashSet<string> preserveSet)
        {
            if (!Directory.Exists(srcDir) || !Directory.Exists(dstDir)) return;
            foreach (var name in preserveSet)
            {
                var src = Path.Combine(srcDir, name);
                var dst = Path.Combine(dstDir, name);
                if (Directory.Exists(src) && !Directory.Exists(dst)) Directory.Move(src, dst);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static bool IsReadyMarker(string path)
        {
            try { return File.Exists(path) && File.ReadAllText(path).Trim() == RestoreReadyMarkerContent; }
            catch { return false; }
        }

        private static void ProbeStagingSharesVolumeOrThrow(string targetDir)
        {
            var probeInside = Path.Combine(targetDir, ".nethereum-volume-probe");
            var probeSibling = TrimDirSeparators(targetDir) + ".nethereum-volume-probe";
            try { if (Directory.Exists(probeInside)) Directory.Delete(probeInside, recursive: true); } catch { }
            try { if (Directory.Exists(probeSibling)) Directory.Delete(probeSibling, recursive: true); } catch { }
            Directory.CreateDirectory(probeInside);
            try
            {
                Directory.Move(probeInside, probeSibling);
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(probeInside)) Directory.Delete(probeInside, recursive: true); } catch { }
                throw new InvalidOperationException(
                    $"Atomic checkpoint restore needs its staging directories (siblings of '{targetDir}') on the same " +
                    $"volume as the data, but they are not — the data dir appears to be its own mount point. Place the " +
                    $"chain data in a SUBDIRECTORY of its volume so the restore staging can share the volume.", ex);
            }
            try { if (Directory.Exists(probeSibling)) Directory.Delete(probeSibling, recursive: true); } catch { }
        }

        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default)
        {
            _rocks.CreateDatabaseCheckpoint(outputPath);
            return Task.CompletedTask;
        }
    }
}
