using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed class RocksDbChainStoreBundle : IChainStoreBundle
    {
        private const string CheckpointArchiveDirName = ".cp";
        private const string CheckpointDirFormat = "D12";

        public IStateStore         State        { get; }
        public ITrieNodeStore      TrieNodes    { get; }
        public IBlockStore         Blocks       { get; }
        public ITransactionStore   Transactions { get; }
        public IUncleStore         Uncles       { get; }
        public IReceiptStore       Receipts     { get; }
        public ILogStore           Logs         { get; }
        public IChainMetadataStore Metadata     { get; }
        public IStateDiffStore     Diffs        { get; }
        public bool                JournalEnabled { get; }
        public string              DataDir      { get; }

        private readonly RocksDbManager _rocks;
        private readonly string _archiveDir;

        private RocksDbChainStoreBundle(
            IStateStore state, ITrieNodeStore trie, IBlockStore blocks,
            ITransactionStore transactions, IUncleStore uncles,
            IReceiptStore receipts, ILogStore logs,
            IChainMetadataStore metadata, IStateDiffStore diffs,
            bool journalEnabled, string dataDir, RocksDbManager rocks)
        {
            State = state;
            TrieNodes = trie;
            Blocks = blocks;
            Transactions = transactions;
            Uncles = uncles;
            Receipts = receipts;
            Logs = logs;
            Metadata = metadata;
            Diffs = diffs;
            JournalEnabled = journalEnabled;
            DataDir = dataDir;
            _rocks = rocks;
            _archiveDir = Path.Combine(dataDir, CheckpointArchiveDirName);
        }

        public static RocksDbChainStoreBundle Open(string dataDir, HistoricalStateOptions journalOptions = null)
        {
            if (string.IsNullOrEmpty(dataDir)) throw new ArgumentException("Data dir required", nameof(dataDir));
            Directory.CreateDirectory(dataDir);
            var options = new RocksDbStorageOptions { DatabasePath = dataDir };
            var rocks = new RocksDbManager(options);
            var blocks = new RocksDbBlockStore(rocks);
            var diffStore = new RocksDbStateDiffStore(rocks);
            IStateStore rawState = new RocksDbStateStore(rocks);
            IStateStore wired = journalOptions != null
                ? new HistoricalStateStore(rawState, diffStore, journalOptions)
                : rawState;
            return new RocksDbChainStoreBundle(
                wired,
                new RocksDbTrieNodeStore(rocks),
                blocks,
                new RocksDbTransactionStore(rocks, blocks),
                new RocksDbUncleStore(rocks, blocks),
                new RocksDbReceiptStore(rocks),
                new RocksDbLogStore(rocks),
                new RocksDbChainMetadataStore(rocks),
                diffStore,
                journalEnabled: journalOptions != null,
                dataDir: dataDir,
                rocks: rocks);
        }

        public string ResolveCheckpointSnapshotPath(ulong blockNumber)
            => Path.Combine(_archiveDir, blockNumber.ToString(CheckpointDirFormat));

        public async Task<ChainCheckpoint> SaveCheckpointAsync(
            ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
        {
            if (stateRoot is null || stateRoot.Length == 0) throw new ArgumentException("stateRoot required", nameof(stateRoot));
            if (blockHash is null || blockHash.Length == 0) throw new ArgumentException("blockHash required", nameof(blockHash));

            var snapshotDir = ResolveCheckpointSnapshotPath(blockNumber);
            var snapshotStaging = snapshotDir + ".staging." + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (Directory.Exists(snapshotDir))
            {
                // Pre-existing snapshot at this height — overwrite atomically.
                // Happens routinely after --rewind-to-checkpoint: the snapshot
                // dir survives the rewind (preserveSubdirs keeps .cp/), and
                // re-execution past the checkpoint block triggers a fresh
                // SaveCheckpointAsync. Refresh the metadata row and skip the
                // expensive RocksDB checkpoint creation since the on-disk
                // snapshot is equivalent (same chain, same state at N).
                Metadata.SaveCheckpoint(blockNumber, stateRoot, blockHash);
                return new ChainCheckpoint(blockNumber, stateRoot, blockHash,
                    (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            Directory.CreateDirectory(_archiveDir);

            try
            {
                _rocks.CreateDatabaseCheckpoint(snapshotStaging);
                ct.ThrowIfCancellationRequested();
                Directory.Move(snapshotStaging, snapshotDir);
            }
            catch
            {
                if (Directory.Exists(snapshotStaging))
                {
                    try { Directory.Delete(snapshotStaging, recursive: true); } catch { }
                }
                throw;
            }

            try
            {
                Metadata.SaveCheckpoint(blockNumber, stateRoot, blockHash);
            }
            catch
            {
                try { Directory.Delete(snapshotDir, recursive: true); } catch { }
                throw;
            }

            await Task.CompletedTask;
            return Metadata.GetCheckpoint(blockNumber)
                   ?? throw new InvalidOperationException(
                       $"Metadata.SaveCheckpoint at {blockNumber} returned but GetCheckpoint reads back null.");
        }

        public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
        {
            var rows = Metadata.ListCheckpointBlockNumbers();
            var result = new List<ChainCheckpoint>(rows.Count);
            foreach (var bn in rows)
            {
                var cp = Metadata.GetCheckpoint(bn);
                if (cp is null) continue;
                if (!Directory.Exists(ResolveCheckpointSnapshotPath(bn))) continue;
                result.Add(cp.Value);
            }
            return Task.FromResult<IReadOnlyList<ChainCheckpoint>>(result);
        }

        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            Metadata.DeleteCheckpoint(blockNumber);
            var snapshotDir = ResolveCheckpointSnapshotPath(blockNumber);
            if (Directory.Exists(snapshotDir))
            {
                try { Directory.Delete(snapshotDir, recursive: true); } catch { }
            }
            return Task.CompletedTask;
        }

        public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            var snapshotDir = ResolveCheckpointSnapshotPath(blockNumber);
            if (!Directory.Exists(snapshotDir))
                throw new InvalidOperationException(
                    $"No snapshot at {snapshotDir} for block {blockNumber}. Use ListCheckpointsAsync to enumerate usable checkpoints.");
            RestoreFromCheckpointDir(snapshotDir, DataDir);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Replace <paramref name="targetDir"/>'s RocksDB contents with a fresh
        /// hard-linked copy of <paramref name="snapshotDir"/>'s RocksDB. Caller
        /// must have disposed any backend currently open on
        /// <paramref name="targetDir"/>. Files in <paramref name="targetDir"/>
        /// are deleted EXCEPT subdirectories listed in
        /// <paramref name="preserveSubdirs"/> (default: <c>.cp</c> so the
        /// snapshot archive survives the restore).
        /// </summary>
        public static void RestoreFromCheckpointDir(
            string snapshotDir,
            string targetDir,
            IEnumerable<string> preserveSubdirs = null)
        {
            if (string.IsNullOrEmpty(snapshotDir)) throw new ArgumentException("snapshotDir required", nameof(snapshotDir));
            if (string.IsNullOrEmpty(targetDir)) throw new ArgumentException("targetDir required", nameof(targetDir));
            snapshotDir = Path.GetFullPath(snapshotDir);
            targetDir = Path.GetFullPath(targetDir);
            if (!Directory.Exists(snapshotDir))
                throw new InvalidOperationException($"Snapshot dir not found: {snapshotDir}");

            var preserveSet = new HashSet<string>(
                preserveSubdirs ?? new[] { CheckpointArchiveDirName },
                StringComparer.OrdinalIgnoreCase);

            var stagingDir = targetDir + ".restoring." + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(stagingDir);
            string effectiveSnapshotDir = snapshotDir;
            var stagedNames = new List<string>();
            try
            {
                if (Directory.Exists(targetDir))
                {
                    foreach (var entry in Directory.EnumerateDirectories(targetDir))
                    {
                        var entryFull = Path.GetFullPath(entry);
                        var name = Path.GetFileName(entryFull);
                        if (!preserveSet.Contains(name)) continue;
                        var stagedPath = Path.Combine(stagingDir, name);
                        Directory.Move(entryFull, stagedPath);
                        stagedNames.Add(name);

                        var entryWithSep = entryFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                           + Path.DirectorySeparatorChar;
                        if (snapshotDir.StartsWith(entryWithSep, StringComparison.OrdinalIgnoreCase))
                        {
                            var relative = snapshotDir.Substring(entryWithSep.Length);
                            effectiveSnapshotDir = Path.Combine(stagedPath, relative);
                        }
                        else if (string.Equals(snapshotDir, entryFull, StringComparison.OrdinalIgnoreCase))
                        {
                            effectiveSnapshotDir = stagedPath;
                        }
                    }
                    Directory.Delete(targetDir, recursive: true);
                }

                if (!Directory.Exists(effectiveSnapshotDir))
                    throw new InvalidOperationException(
                        $"Snapshot dir not found at expected location after staging: {effectiveSnapshotDir} " +
                        $"(originally {snapshotDir}).");

                var options = new RocksDbStorageOptions { DatabasePath = effectiveSnapshotDir };
                using (var temp = new RocksDbManager(options))
                {
                    temp.CreateDatabaseCheckpoint(targetDir);
                }

                // On partial move-back failure, keep stagingDir alive so the
                // archive isn't lost — the finally block at the end of the
                // method only deletes stagingDir when every preserved subdir
                // successfully moved back into targetDir.
                bool allRestored = true;
                List<string> restoreFailures = null;
                foreach (var name in stagedNames)
                {
                    try
                    {
                        Directory.Move(Path.Combine(stagingDir, name), Path.Combine(targetDir, name));
                    }
                    catch (Exception ex)
                    {
                        allRestored = false;
                        restoreFailures ??= new List<string>();
                        restoreFailures.Add($"{name}: {ex.Message}");
                    }
                }
                if (!allRestored)
                {
                    var recoveryDir = targetDir + ".restore-failed." + Guid.NewGuid().ToString("N").Substring(0, 8);
                    try
                    {
                        if (Directory.Exists(stagingDir)) Directory.Move(stagingDir, recoveryDir);
                    }
                    catch { }
                    throw new InvalidOperationException(
                        $"Partial restore of preserved subdirs failed: [{string.Join(", ", restoreFailures)}]. " +
                        $"Surviving archive moved to {recoveryDir} (or left at {stagingDir} if rename also failed) — " +
                        $"do NOT remove until the contents are merged into {targetDir}.");
                }
            }
            finally
            {
                if (Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, recursive: true); } catch { }
                }
            }
        }

        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default)
        {
            _rocks.CreateDatabaseCheckpoint(outputPath);
            return Task.CompletedTask;
        }

        public Task ResetStateOnlyAsync(CancellationToken ct = default)
        {
            // Order: metadata first (cheap), then bulk-wipe state CFs. Each
            // WipeColumnFamily is bounded-batch internally; total time scales
            // with total state size but stays predictable for any DB.
            Metadata.ResetForStateRebuild();
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_CODE);
            _rocks.WipeColumnFamily(RocksDbManager.CF_TRIE_NODES);
            _rocks.WipeColumnFamily(RocksDbManager.CF_BINARY_TRIE_NODES);
            _rocks.WipeColumnFamily(RocksDbManager.CF_BINARY_TRIE_DEPTH_IDX);
            _rocks.WipeColumnFamily(RocksDbManager.CF_BINARY_TRIE_ADDR_STEMS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_RECEIPTS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_LOGS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_LOG_BY_BLOCK);
            _rocks.WipeColumnFamily(RocksDbManager.CF_LOG_BY_ADDRESS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_LOG_BY_TX);
            _rocks.WipeColumnFamily(RocksDbManager.CF_RECEIPT_BY_BLOCK);
            _rocks.WipeColumnFamily(RocksDbManager.CF_BLOCK_BLOOMS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);
            _rocks.WipeColumnFamily(RocksDbManager.CF_FILTERS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_MSG_RESULTS);
            _rocks.WipeColumnFamily(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);
            _rocks.Flush();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            (State as IDisposable)?.Dispose();
            _rocks?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
