using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Default <see cref="IFollowerService"/>. Pulls bundles from the
    /// source, executes them through <see cref="IBlockExecutor"/>, commits
    /// the cursor per matched block, periodically anchors via
    /// <see cref="ICanonicalStateRootSource"/> when the policy says to, and
    /// routes divergence outcomes through <see cref="IValidationPolicy"/>.
    /// Journal-rewinds are handled in-process (executor is rebuilt against
    /// the same bundle, loop resumes). Snapshot-rewinds return
    /// <see cref="FollowerExitReason.SnapshotRestoreRequested"/>: the caller
    /// performs the dispose/restore/reopen externally and re-invokes
    /// <see cref="RunAsync"/>.
    /// </summary>
    public sealed class FollowerService : IFollowerService
    {
        public async Task<FollowerRunResult> RunAsync(
            IBlockSource source,
            Func<IChainStoreBundle> bundleFactory,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            CancellationToken ct,
            ILogger logger = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (bundleFactory is null) throw new ArgumentNullException(nameof(bundleFactory));
            if (executorFactory is null) throw new ArgumentNullException(nameof(executorFactory));
            if (policy is null) throw new ArgumentNullException(nameof(policy));
            if (options is null) throw new ArgumentNullException(nameof(options));
            logger ??= NullLogger.Instance;

            var bundle = bundleFactory();
            var executor = executorFactory(bundle);

            ulong lastCommittedBlock = bundle.Metadata.GetLastBlock();
            byte[] lastCommittedHash = bundle.Metadata.GetLastBlockHash() ?? new byte[32];
            ulong blocksExecuted = 0UL;
            ulong rootMismatches = 0UL;
            ulong rewindCyclesUsed = 0UL;
            int consecutiveDivergences = 0;
            ulong currentStart = options.StartBlock;

            try
            {
                int consecutiveSourceFailures = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    bool restartLoop = false;

                    IAsyncEnumerable<BlockBundle> stream;
                    try
                    {
                        stream = source.StreamAsync(currentStart, ct);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        // Source itself threw synchronously when starting the stream.
                        // Same recovery as in-flight failure below.
                        consecutiveSourceFailures++;
                        if (consecutiveSourceFailures > options.MaxConsecutiveSourceFailures)
                        {
                            return new FollowerRunResult(
                                FollowerExitReason.FatalVerdict,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: $"source persistently unavailable ({ex.GetType().Name}: {ex.Message}) after {consecutiveSourceFailures} attempts");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                        continue;
                    }

                    var enumerator = stream.GetAsyncEnumerator(ct);
                    try
                    {
                        while (true)
                        {
                            bool hasNext;
                            try
                            {
                                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex) when (!ct.IsCancellationRequested)
                            {
                                // Transient source failure mid-stream (FetchRequestFailedException,
                                // IOException, TimeoutException, etc.) Absorb and restart the
                                // outer loop after a backoff — the cursor `currentStart` is the
                                // next block we still need, so resuming from there picks up
                                // exactly where we left off without losing in-flight progress.
                                consecutiveSourceFailures++;
                                if (consecutiveSourceFailures > options.MaxConsecutiveSourceFailures)
                                {
                                    return new FollowerRunResult(
                                        FollowerExitReason.FatalVerdict,
                                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                        SnapshotRestoreTarget: null,
                                        Detail: $"source persistently failing ({ex.GetType().Name}: {ex.Message}) after {consecutiveSourceFailures} consecutive attempts");
                                }
                                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                                restartLoop = true;
                                break;
                            }
                            if (!hasNext) break;
                            consecutiveSourceFailures = 0;
                            var blockBundle = enumerator.Current;
                        ct.ThrowIfCancellationRequested();

                        var header = blockBundle.Header;
                        var blockNumber = (ulong)header.BlockNumber;

                        var result = await executor.ProcessBlockAsync(
                            header,
                            blockBundle.Transactions,
                            blockBundle.Uncles,
                            WithdrawalAdapter.Convert(blockBundle.Withdrawals),
                            ct).ConfigureAwait(false);

                        if (result.RootMatches)
                        {
                            lastCommittedBlock = blockNumber;
                            lastCommittedHash = blockBundle.HeaderHash;
                            currentStart = blockNumber + 1;
                            blocksExecuted++;
                            consecutiveDivergences = 0;
                            bundle.Metadata.Commit(lastCommittedBlock, lastCommittedHash);

                            if (options.CheckpointEvery > 0
                                && lastCommittedBlock > 0
                                && lastCommittedBlock % options.CheckpointEvery == 0)
                            {
                                // The block IS committed at line above (Metadata.Commit) —
                                // the checkpoint is an optimisation for future rewind, not
                                // a correctness invariant. A disk-full / locked-file /
                                // permissions error on snapshot creation must not fatal
                                // the live sync (matches the re-execute path's
                                // try/catch around the same call).
                                bool checkpointSaved = false;
                                try
                                {
                                    await bundle.SaveCheckpointAsync(
                                        lastCommittedBlock, result.ComputedStateRoot, lastCommittedHash, ct)
                                        .ConfigureAwait(false);
                                    checkpointSaved = true;
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (System.Exception cpEx)
                                {
                                    logger.LogWarning(cpEx,
                                        "Checkpoint save failed at block {block}; sync continues without this checkpoint.",
                                        lastCommittedBlock);
                                }

                                // Auto-prune older checkpoints once a new one is durably
                                // on disk. Unbounded accumulation exhausted ~90 GB on a
                                // recent mainnet run (29 cps × ~3-7 GB each); the cap
                                // bounds steady-state usage at roughly
                                // (KeepLatestCheckpoints × per-cp-size).
                                if (checkpointSaved
                                    && options.KeepLatestCheckpoints.HasValue
                                    && options.KeepLatestCheckpoints.Value > 0)
                                {
                                    await PruneOlderCheckpointsAsync(
                                        bundle, options.KeepLatestCheckpoints.Value, logger, ct)
                                        .ConfigureAwait(false);
                                }
                            }

                            if (canonical != null && policy.ShouldAnchorAt(lastCommittedBlock))
                            {
                                DivergenceVerdict anchorVerdict;
                                try
                                {
                                    anchorVerdict = await canonical
                                        .DiagnoseAsync(lastCommittedBlock, result.ComputedStateRoot, result.ComputedStateRoot, ct)
                                        .ConfigureAwait(false);
                                }
                                catch (System.Exception ex)
                                {
                                    anchorVerdict = new DivergenceVerdict(
                                        DivergenceOutcome.SourceUnavailable, null, null, canonical.Name,
                                        $"anchor source threw {ex.GetType().Name}: {ex.Message}");
                                }

                                bool anchorMatched = anchorVerdict.Outcome != DivergenceOutcome.SourceUnavailable
                                    && ByteUtil.AreEqual(anchorVerdict.CanonicalStateRoot, result.ComputedStateRoot);
                                if (anchorMatched)
                                {
                                    logger.LogInformation(
                                        "anchor check PASS: block={Block} source={Source}",
                                        lastCommittedBlock, anchorVerdict.SourceName);
                                }
                                else if (anchorVerdict.Outcome == DivergenceOutcome.SourceUnavailable)
                                {
                                    logger.LogDebug(
                                        "anchor check skipped: block={Block} detail={Detail}",
                                        lastCommittedBlock, anchorVerdict.Detail);
                                }

                                if (anchorVerdict.Outcome != DivergenceOutcome.SourceUnavailable
                                    && !ByteUtil.AreEqual(anchorVerdict.CanonicalStateRoot, result.ComputedStateRoot))
                                {
                                    rootMismatches++;
                                    consecutiveDivergences++;
                                    if (consecutiveDivergences > options.MaxConsecutiveDivergences)
                                    {
                                        return new FollowerRunResult(
                                            FollowerExitReason.FatalVerdict,
                                            lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                            SnapshotRestoreTarget: null,
                                            Detail: $"max consecutive divergences ({options.MaxConsecutiveDivergences}) " +
                                                    $"exceeded at periodic anchor check on block {lastCommittedBlock:N0}");
                                    }

                                    var anchorAction = policy.OnVerdict(anchorVerdict, lastCommittedBlock);
                                    switch (anchorAction)
                                    {
                                        case ValidationAction.Continue:
                                            break;
                                        case ValidationAction.Fatal:
                                            return new FollowerRunResult(
                                                FollowerExitReason.FatalVerdict,
                                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                                SnapshotRestoreTarget: null,
                                                Detail: $"periodic anchor fatal at block {lastCommittedBlock:N0}: {anchorVerdict.Detail}");
                                        case ValidationAction.RewindAndRetry:
                                            var anchorRewindOutcome = await ValidatingRewindAsync(
                                                bundle, canonical, options,
                                                lastCommittedBlock, lastCommittedHash,
                                                blocksExecuted, rootMismatches, rewindCyclesUsed,
                                                ct).ConfigureAwait(false);

                                            rewindCyclesUsed = anchorRewindOutcome.RewindCyclesUsed;
                                            if (anchorRewindOutcome.TerminalResult is FollowerRunResult anchorTerminal)
                                            {
                                                return anchorTerminal;
                                            }

                                            executor = executorFactory(bundle);
                                            currentStart = anchorRewindOutcome.NewHead + 1;
                                            lastCommittedBlock = anchorRewindOutcome.NewHead;
                                            lastCommittedHash = anchorRewindOutcome.NewHeadHash ?? lastCommittedHash;
                                            restartLoop = true;
                                            break;
                                    }

                                    if (restartLoop) break;
                                }
                            }

                            if (options.EndBlock.HasValue && lastCommittedBlock >= options.EndBlock.Value)
                            {
                                return new FollowerRunResult(
                                    FollowerExitReason.SourceCompleted,
                                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                    SnapshotRestoreTarget: null,
                                    Detail: $"reached EndBlock={options.EndBlock.Value:N0}");
                            }
                            continue;
                        }

                        rootMismatches++;
                        consecutiveDivergences++;
                        if (consecutiveDivergences > options.MaxConsecutiveDivergences)
                        {
                            return new FollowerRunResult(
                                FollowerExitReason.FatalVerdict,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: $"max consecutive divergences ({options.MaxConsecutiveDivergences}) exceeded at block {blockNumber:N0}");
                        }

                        DivergenceVerdict verdict;
                        if (canonical != null)
                        {
                            try
                            {
                                verdict = await canonical
                                    .DiagnoseAsync(blockNumber, result.ExpectedStateRoot, result.ComputedStateRoot, ct)
                                    .ConfigureAwait(false);
                            }
                            catch (System.Exception ex)
                            {
                                verdict = new DivergenceVerdict(
                                    DivergenceOutcome.SourceUnavailable, null, null, canonical.Name,
                                    $"canonical source threw {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                        else
                        {
                            verdict = new DivergenceVerdict(
                                DivergenceOutcome.SourceUnavailable, null, null, "<none>",
                                "no canonical source wired; state-root mismatch only");
                        }

                        var action = policy.OnVerdict(verdict, blockNumber);
                        await source.ReportBadBundleAsync(blockNumber, BadBundleReason.StateRootMismatch, ct)
                            .ConfigureAwait(false);

                        switch (action)
                        {
                            case ValidationAction.Continue:
                                continue;

                            case ValidationAction.Fatal:
                                return new FollowerRunResult(
                                    FollowerExitReason.FatalVerdict,
                                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                    SnapshotRestoreTarget: null,
                                    Detail: $"fatal verdict at block {blockNumber:N0}: {verdict.Detail}");

                            case ValidationAction.RewindAndRetry:
                                {
                                    var rewindOutcome = await ValidatingRewindAsync(
                                        bundle, canonical, options,
                                        lastCommittedBlock, lastCommittedHash,
                                        blocksExecuted, rootMismatches, rewindCyclesUsed,
                                        ct).ConfigureAwait(false);

                                    rewindCyclesUsed = rewindOutcome.RewindCyclesUsed;
                                    if (rewindOutcome.TerminalResult is FollowerRunResult terminal)
                                    {
                                        return terminal;
                                    }

                                    executor = executorFactory(bundle);
                                    currentStart = rewindOutcome.NewHead + 1;
                                    lastCommittedBlock = rewindOutcome.NewHead;
                                    lastCommittedHash = rewindOutcome.NewHeadHash ?? lastCommittedHash;
                                    restartLoop = true;
                                }
                                break;
                        }

                        if (restartLoop) break;
                        }  // close inner while(true) MoveNextAsync loop
                    }
                    finally
                    {
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                    }

                    if (!restartLoop && source.LastChainBreak is not null)
                    {
                        var cb = source.LastChainBreak;
                        var cbVerdict = new DivergenceVerdict(
                            DivergenceOutcome.SourceUnavailable,
                            cb.PeerParentHash,
                            cb.OurParentHash,
                            cb.SourceName ?? "<source>",
                            $"chain-break at {cb.AtBlock:N0}: {cb.QuorumPeerCount} peer(s) report parent " +
                            $"0x{cb.PeerParentHash.ToHex().Substring(0, 16)}…; local parent " +
                            $"0x{cb.OurParentHash.ToHex().Substring(0, 16)}…");
                        var cbAction = policy.OnVerdict(cbVerdict, cb.AtBlock);
                        if (cbAction == ValidationAction.Fatal)
                        {
                            return new FollowerRunResult(
                                FollowerExitReason.FatalVerdict,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: $"chain-break fatal at {cb.AtBlock:N0}: {cbVerdict.Detail}");
                        }
                        if (cbAction == ValidationAction.RewindAndRetry)
                        {
                            var cbRewindOutcome = await ValidatingRewindAsync(
                                bundle, canonical, options,
                                lastCommittedBlock, lastCommittedHash,
                                blocksExecuted, rootMismatches, rewindCyclesUsed,
                                ct).ConfigureAwait(false);

                            rewindCyclesUsed = cbRewindOutcome.RewindCyclesUsed;
                            if (cbRewindOutcome.TerminalResult is FollowerRunResult cbTerminal)
                            {
                                return cbTerminal;
                            }

                            executor = executorFactory(bundle);
                            currentStart = cbRewindOutcome.NewHead + 1;
                            lastCommittedBlock = cbRewindOutcome.NewHead;
                            lastCommittedHash = cbRewindOutcome.NewHeadHash ?? lastCommittedHash;
                            restartLoop = true;
                        }
                    }

                    if (!restartLoop) break;
                }

                return new FollowerRunResult(
                    FollowerExitReason.SourceCompleted,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    SnapshotRestoreTarget: null,
                    Detail: "source stream completed");
            }
            catch (OperationCanceledException)
            {
                return new FollowerRunResult(
                    FollowerExitReason.Cancelled,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    SnapshotRestoreTarget: null,
                    Detail: "cancelled");
            }
            finally
            {
                if (bundle != null && lastCommittedBlock > 0)
                {
                    try
                    {
                        bundle.Metadata.Commit(lastCommittedBlock, lastCommittedHash);
                    }
                    catch (Exception ex)
                    {
                        // The earlier inner-loop commits are what the consumer relied
                        // on; this finally-block re-commit is defence-in-depth. A
                        // failure here means metadata IO is broken (disk full,
                        // permissions, DB closed mid-shutdown). Swallowing left no
                        // signal — log so post-mortems can correlate.
                        logger?.LogError(ex,
                            "FollowerService finally-block Commit({block}, {hash}) failed; metadata may be stale on next start.",
                            lastCommittedBlock,
                            lastCommittedHash != null ? lastCommittedHash.ToHex() : "(null)");
                    }
                }
            }
        }

        /// <summary>
        /// Step-by-step canonical-confirmed rewind. When a canonical source is
        /// available, rewinds one block at a time and asks the canonical source
        /// whether the resulting head's state root agrees. If it does, returns
        /// that head as the resume point; if not, rewinds another step and
        /// repeats until <see cref="FollowerOptions.MaxRewindCycles"/> is
        /// exhausted. When canonical is unavailable (or returns no answer),
        /// falls back to a single rewind to <c>lastCommittedBlock - 1</c>,
        /// preserving prior behaviour.
        /// </summary>
        private static async Task<RewindLoopOutcome> ValidatingRewindAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct)
        {
            var coordinator = new RewindCoordinator(bundle);
            ulong currentHead = lastCommittedBlock;
            byte[] currentHash = lastCommittedHash;
            bool fallBackToSingleShot = canonical == null;

            while (true)
            {
                rewindCyclesUsed++;
                if (rewindCyclesUsed > (ulong)options.MaxRewindCycles)
                {
                    return RewindLoopOutcome.Terminal(
                        rewindCyclesUsed,
                        BuildMaxRewindFatal(lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed, options, currentHead));
                }

                if (!fallBackToSingleShot)
                {
                    var verdict = await ConfirmCanonicalAtHeadAsync(bundle, canonical, currentHead, ct)
                        .ConfigureAwait(false);
                    if (verdict.SourceFailed)
                    {
                        fallBackToSingleShot = true;
                    }
                    else if (verdict.Match)
                    {
                        return RewindLoopOutcome.Resume(rewindCyclesUsed, currentHead, currentHash);
                    }
                    else if (currentHead == 0)
                    {
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed,
                            new FollowerRunResult(
                                FollowerExitReason.FatalVerdict,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: $"validating rewind reached block 0 with state root " +
                                        $"0x{(verdict.OurRoot ?? System.Array.Empty<byte>()).ToHex()} that disagrees with " +
                                        $"canonical 0x{(verdict.CanonicalRoot ?? System.Array.Empty<byte>()).ToHex()} via {canonical.Name}"));
                    }
                }

                ulong rewindTarget = currentHead > 0 ? currentHead - 1 : 0;
                var rewindResult = await coordinator.RewindToAsync(
                    rewindTarget, RewindPolicy.JournalFirstThenSnapshot, ct).ConfigureAwait(false);

                switch (rewindResult.Outcome)
                {
                    case RewindOutcome.JournalUsed:
                        currentHead = rewindResult.NewHead;
                        currentHash = bundle.Metadata.GetLastBlockHash() ?? currentHash;
                        if (fallBackToSingleShot)
                        {
                            return RewindLoopOutcome.Resume(rewindCyclesUsed, currentHead, currentHash);
                        }
                        continue;

                    case RewindOutcome.SnapshotUsed:
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed,
                            new FollowerRunResult(
                                FollowerExitReason.SnapshotRestoreRequested,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: rewindResult.RestoredCheckpoint,
                                Detail: rewindResult.Detail));

                    case RewindOutcome.NoOp:
                    case RewindOutcome.NoPathAvailable:
                    default:
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed,
                            new FollowerRunResult(
                                FollowerExitReason.RewindUnavailable,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: rewindResult.Detail));
                }
            }
        }

        private static FollowerRunResult BuildMaxRewindFatal(
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches,
            ulong rewindCyclesUsed, FollowerOptions options, ulong currentHead)
            => new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                SnapshotRestoreTarget: null,
                Detail: $"exceeded MaxRewindCycles ({options.MaxRewindCycles}) " +
                        $"during validating rewind at head {currentHead:N0}");

        private readonly struct CanonicalVerdict
        {
            public bool Match { get; }
            public bool SourceFailed { get; }
            public byte[] OurRoot { get; }
            public byte[] CanonicalRoot { get; }
            public CanonicalVerdict(bool match, bool sourceFailed, byte[] ourRoot, byte[] canonicalRoot)
            {
                Match = match;
                SourceFailed = sourceFailed;
                OurRoot = ourRoot;
                CanonicalRoot = canonicalRoot;
            }
        }

        private static async Task<CanonicalVerdict> ConfirmCanonicalAtHeadAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            ulong head,
            CancellationToken ct)
        {
            var oursAtHead = await ReadOurStateRootAtAsync(bundle, head, ct).ConfigureAwait(false);
            if (oursAtHead == null)
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, null, null);
            }

            byte[] canonicalRoot;
            try
            {
                var (root, _) = await canonical.GetCanonicalAsync(head, ct).ConfigureAwait(false);
                canonicalRoot = root;
            }
            catch
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, oursAtHead, null);
            }

            if (canonicalRoot == null)
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, oursAtHead, null);
            }

            return new CanonicalVerdict(
                match: ByteSequenceEqual(oursAtHead, canonicalRoot),
                sourceFailed: false,
                oursAtHead,
                canonicalRoot);
        }

        private static async Task<byte[]> ReadOurStateRootAtAsync(
            IChainStoreBundle bundle, ulong blockNumber, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var header = await bundle.Blocks.GetByNumberAsync(blockNumber).ConfigureAwait(false);
            return header?.StateRoot;
        }

        private static bool ByteSequenceEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        // Trims the on-disk checkpoint archive down to the most recent N. Called
        // after every successful SaveCheckpointAsync when KeepLatestCheckpoints is
        // set. Each delete is best-effort; a failure on one row doesn't stop the
        // others — disk pressure is the reason we're pruning at all and partial
        // progress is better than none.
        private static async Task PruneOlderCheckpointsAsync(
            IChainStoreBundle bundle, int keepLatest, ILogger logger, CancellationToken ct)
        {
            var existing = bundle.Metadata.ListCheckpointBlockNumbers();
            if (existing.Count <= keepLatest) return;

            // ListCheckpointBlockNumbers returns ascending; the tail is the most
            // recent so we want to drop everything BEFORE the last keepLatest entries.
            int dropCount = existing.Count - keepLatest;
            for (int i = 0; i < dropCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                ulong bn = existing[i];
                try
                {
                    await bundle.DeleteCheckpointAsync(bn).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    logger.LogWarning(ex,
                        "Auto-prune: DeleteCheckpointAsync({block}) failed; sync continues.", bn);
                }
            }
            logger.LogInformation(
                "Auto-prune: dropped {dropped} checkpoint(s) below the latest {keep}.",
                dropCount, keepLatest);
        }

        private readonly struct RewindLoopOutcome
        {
            public ulong RewindCyclesUsed { get; }
            public ulong NewHead { get; }
            public byte[] NewHeadHash { get; }
            public FollowerRunResult TerminalResult { get; }

            private RewindLoopOutcome(
                ulong rewindCyclesUsed,
                ulong newHead,
                byte[] newHeadHash,
                FollowerRunResult terminalResult)
            {
                RewindCyclesUsed = rewindCyclesUsed;
                NewHead = newHead;
                NewHeadHash = newHeadHash;
                TerminalResult = terminalResult;
            }

            public static RewindLoopOutcome Resume(
                ulong rewindCyclesUsed, ulong newHead, byte[] newHeadHash)
                => new RewindLoopOutcome(rewindCyclesUsed, newHead, newHeadHash, null);

            public static RewindLoopOutcome Terminal(
                ulong rewindCyclesUsed, FollowerRunResult terminalResult)
                => new RewindLoopOutcome(rewindCyclesUsed, 0, null, terminalResult);
        }
    }
}
