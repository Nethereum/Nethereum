using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed class BootRecoveryGate
    {
        public const string RestoreRequestFileName = "restore-checkpoint.request";

        public const string CleanShutdownMarkerFileName = "clean-shutdown.ok";

        private readonly ILogger _logger;

        public BootRecoveryGate(ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private static string ResolveRestoreRequestMarkerPath(string dataDir) =>
            dataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "." + RestoreRequestFileName;

        public void ApplyPendingRestore(string dataDir, string freezerHistoryDirectory = null, bool promotionEnabled = false)
        {
            if (string.IsNullOrEmpty(dataDir)) return;

            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(dataDir);

            var marker = ResolveRestoreRequestMarkerPath(dataDir);
            if (!File.Exists(marker))
            {
                var legacy = Path.Combine(dataDir, RestoreRequestFileName);
                if (!File.Exists(legacy)) return;
                marker = legacy;
            }

            var text = File.ReadAllText(marker).Trim();
            if (!ulong.TryParse(text, out var blockNumber))
            {
                _logger.LogError(
                    "Checkpoint-restore marker {Marker} is unreadable (content: '{Content}'); renaming aside and booting on existing data.",
                    marker, text);
                File.Move(marker, marker + ".invalid", overwrite: true);
                return;
            }

            var snapshotDir = Nethereum.CoreChain.RocksDB.Stores.RocksDbCheckpointManager.ResolveCheckpointSnapshotPath(dataDir, blockNumber);
            if (!Directory.Exists(snapshotDir))
            {
                _logger.LogError(
                    "Checkpoint-restore marker requests block {Block} but no snapshot exists at {SnapshotDir}; renaming aside and booting on existing data.",
                    blockNumber, snapshotDir);
                File.Move(marker, marker + ".invalid", overwrite: true);
                return;
            }

            _logger.LogWarning(
                "Applying requested checkpoint restore: block {Block} from {SnapshotDir} (data dir contents are replaced; the snapshot archive is preserved).",
                blockNumber, snapshotDir);
            try
            {
                Nethereum.CoreChain.RocksDB.Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(
                    snapshotDir, dataDir, promotionEnabled: promotionEnabled, freezerDirectory: freezerHistoryDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "Requested checkpoint restore to block {Block} could not be applied; the data dir was left intact " +
                    "and the node is booting on it. If the data dir is a dedicated mount point, place the chain data " +
                    "in a SUBDIRECTORY of its volume so the atomic restore staging can share the volume. Request " +
                    "renamed aside to {Marker}.failed.", blockNumber, marker);
                try { File.Move(marker, marker + ".failed", overwrite: true); } catch { }
                return;
            }
            File.Delete(marker);
            _logger.LogWarning("Checkpoint restore complete: node state is at block {Block}.", blockNumber);
        }

        public void EnsureConsistentOrEscalate(RocksDbChainStoreBundle bundle, string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir)) return;

            var marker = Path.Combine(dataDir, CleanShutdownMarkerFileName);
            if (File.Exists(marker))
            {
                try { File.Delete(marker); } catch { }
            }

            try
            {
                var (head, recovered) = bundle
                    .EnsureConsistentHeadAsync(msg => _logger.LogWarning("boot.integrity {Msg}", msg))
                    .GetAwaiter().GetResult();
                if (recovered)
                    _logger.LogWarning(
                        "Boot integrity gate: an unclean shutdown left a torn head; recovered to block {Block} via node history before serving.",
                        head);
                else
                    _logger.LogInformation(
                        "Boot integrity gate: committed head {Block} resolves cleanly (no repair needed).", head);
            }
            catch (Exception ex)
            {
                var checkpoint = bundle.Metadata.GetLatestCheckpoint();
                var snapshotDir = checkpoint > 0
                    ? Nethereum.CoreChain.RocksDB.Stores.RocksDbCheckpointManager.ResolveCheckpointSnapshotPath(dataDir, checkpoint)
                    : null;

                if (snapshotDir != null && Directory.Exists(snapshotDir))
                {
                    _logger.LogCritical(ex,
                        "Boot integrity gate: torn head could not be self-healed from node history. Requesting a " +
                        "checkpoint restore to block {Block} on the next start.", checkpoint);
                    if (RecordRestoreRequest(dataDir, checkpoint))
                        throw;
                }
                else
                {
                    _logger.LogCritical(ex,
                        "Boot integrity gate: torn head could not be self-healed and NO usable checkpoint snapshot " +
                        "exists to restore. Booting on the last committed state — the follower will halt on the first " +
                        "divergence and RPC keeps serving. Manual recovery required (node-history CLI recovery or re-sync).");
                }
            }
        }

        public bool RecordRestoreRequest(string dataDir, ulong blockNumber)
        {
            if (string.IsNullOrEmpty(dataDir)) return false;
            try
            {
                File.WriteAllText(
                    ResolveRestoreRequestMarkerPath(dataDir),
                    blockNumber.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "Failed to record the checkpoint-restore request for block {Block}; booting on the last committed " +
                    "state instead of crash-looping. Restore must be run manually.", blockNumber);
                return false;
            }
        }

        public void MarkCleanShutdown(string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir)) return;
            try
            {
                File.WriteAllText(
                    Path.Combine(dataDir, CleanShutdownMarkerFileName),
                    DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            }
            catch { }
        }
    }
}
