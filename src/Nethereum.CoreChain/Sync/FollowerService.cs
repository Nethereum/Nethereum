using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
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
    ///
    /// <para>
    /// When constructed with a <see cref="BackwardWalkerDelegate"/>, RunAsync
    /// switches to a tip-event-driven main loop after the snap-pivot
    /// fast-start: it polls the supplied <see cref="ICanonicalStateRootSource"/>
    /// every <see cref="FollowerOptions.TipPollInterval"/> and invokes the
    /// walker on each non-trivial tip advance, then runs the forward executor
    /// against locally-stored blocks the walker filled.
    /// When constructed with a non-null <see cref="AncestorResolverDelegate"/>,
    /// the loop also handles reorg recovery: on
    /// <see cref="WalkerExitReason.LastKnownGoodDivergence"/> it binary-searches
    /// for the last common ancestor, rewinds the fetch cursors to that point,
    /// and retries the tip poll. When the resolver is null the loop preserves
    /// the prior behaviour: log and halt on divergence.
    /// </para>
    /// </summary>
    public sealed class FollowerService : IFollowerService
    {
        private readonly BackwardWalkerDelegate _walker;
        private readonly AncestorResolverDelegate _ancestorResolver;

        /// <summary>
        /// Stateless follower — the legacy behavior. RunAsync consumes
        /// <see cref="IBlockSource.StreamAsync"/> forward from the cursor.
        /// </summary>
        public FollowerService() : this(null, null) { }

        /// <summary>
        /// Tip-event-driven follower. <paramref name="walker"/> is invoked on every
        /// canonical-tip advance past the local cursor; the executor then runs forward
        /// over the locally-stored skeleton via an internal
        /// <see cref="OrderingBlockSource"/> adapter. Pass null to fall back to the
        /// legacy <see cref="IBlockSource"/>-driven path. The delegate shape mirrors
        /// <c>Nethereum.DevP2P.Sync.IBackwardBlockWalker.WalkAsync</c>; CoreChain stays
        /// free of a DevP2P.Sync reference by going through this delegate.
        /// </summary>
        public FollowerService(BackwardWalkerDelegate walker) : this(walker, null) { }

        /// <summary>
        /// Tip-event-driven follower with reorg recovery. <paramref name="ancestorResolver"/>
        /// is invoked when the walker exits with
        /// <see cref="WalkerExitReason.LastKnownGoodDivergence"/>: binary-search the
        /// canonical chain for the last common ancestor, rewind the fetch cursors to
        /// that block, and retry the tip poll. Pass null to preserve the
        /// "log + halt on divergence" behaviour.
        /// </summary>
        public FollowerService(BackwardWalkerDelegate walker, AncestorResolverDelegate ancestorResolver)
        {
            _walker = walker;
            _ancestorResolver = ancestorResolver;
        }

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

            // Snap-pivot-aware start: when a successful snap bootstrap has
            // populated the state store at the pivot block but the executor
            // hasn't run yet (LastBlock unmoved past the pivot), starting at
            // block 1 against pivot-state would diverge on every block until
            // it catches up. The canonical snap-sync flow splits fetched
            // results around the pivot and only feeds post-pivot blocks to the
            // executor — pre-pivot blocks are just archived. We do the same:
            // treat the pivot as already-executed
            // and tell the source to stream from pivot+1 onwards.
            var snapState = bundle.Metadata.GetSnapSyncState();
            if (snapState is not null && snapState.Phase == SnapPhase.Complete)
            {
                // Re-read the committed-block cursor in a tight window with
                // the snap-state read. The bootstrapper writes Phase=Complete
                // and Metadata.Commit(pivot) atomically in a single batch, so
                // a snap-state showing Complete is paired with a fresh cursor.
                // Comparing pivot against the stale snapshot captured at the
                // top of RunAsync would mis-fire fast-start when the
                // bootstrapper finished between the two reads.
                var freshLastBlock = bundle.Metadata.GetLastBlock();
                if (snapState.PivotBlockNumber > freshLastBlock)
                {
                    lastCommittedBlock = snapState.PivotBlockNumber;
                    lastCommittedHash = snapState.PivotBlockHash ?? lastCommittedHash;
                    if (currentStart <= snapState.PivotBlockNumber)
                    {
                        currentStart = snapState.PivotBlockNumber + 1;
                    }
                    logger.LogInformation(
                        "FollowerService: snap pivot detected at block {Pivot}; treating as executed and starting at {Start}",
                        snapState.PivotBlockNumber, currentStart);
                }
                else if (freshLastBlock > lastCommittedBlock)
                {
                    lastCommittedBlock = freshLastBlock;
                    lastCommittedHash = bundle.Metadata.GetLastBlockHash() ?? lastCommittedHash;
                }
            }

            if (_walker != null && canonical != null)
            {
                logger.LogInformation(
                    "follower.path active=tip-driven walker_wired=true canonical_wired=true");
                return await RunTipDrivenAsync(
                    bundle, executor, executorFactory, policy, canonical, options,
                    lastCommittedBlock, lastCommittedHash, currentStart,
                    ct, logger).ConfigureAwait(false);
            }

            logger.LogInformation(
                "follower.path active=legacy-stream walker_null={WalkerNull} canonical_null={CanonicalNull}",
                _walker == null, canonical == null);

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
        /// Tip-event-driven main loop. Polls
        /// <see cref="ICanonicalStateRootSource.GetLatestAsync"/> every
        /// <see cref="FollowerOptions.TipPollInterval"/>; on each non-trivial
        /// delta past the local cursor, invokes <see cref="_walker"/>
        /// (skeleton-fills headers + bodies backward from the tip into local
        /// storage), then runs the forward executor over the locally-stored
        /// range via an internal <see cref="OrderingBlockSource"/> adapter.
        /// Loops until cancellation or a fatal walker outcome.
        /// </summary>
        private async Task<FollowerRunResult> RunTipDrivenAsync(
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong currentStart,
            CancellationToken ct,
            ILogger logger)
        {
            var pollInterval = options.TipPollInterval ?? TimeSpan.FromSeconds(12);
            ulong blocksExecuted = 0UL;
            ulong rootMismatches = 0UL;
            ulong lastSeenTipBlock = 0UL;
            byte[] lastSeenTipHash = null;
            int consecutiveSourceFailures = 0;

            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    CanonicalTip tip = null;
                    Exception lastSourceException = null;
                    try
                    {
                        tip = await canonical.GetLatestAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        lastSourceException = ex;
                        logger.LogDebug(ex,
                            "snap.canonical.poll_failed source={Source}; retrying in {Interval}",
                            canonical.Name, pollInterval);
                    }

                    if (tip == null)
                    {
                        // GetLatestAsync produced no tip (either threw above or returned
                        // null). A misconfigured canonical source would otherwise spin
                        // here forever; cap the streak using the shared budget that the
                        // non-tip-driven path enforces at line 146 / 176 above. Each
                        // miss counts; a successful poll resets the counter below.
                        consecutiveSourceFailures++;
                        if (consecutiveSourceFailures > options.MaxConsecutiveSourceFailures)
                        {
                            var reason = lastSourceException != null
                                ? $"{lastSourceException.GetType().Name}: {lastSourceException.Message}"
                                : "GetLatestAsync returned null";
                            logger.LogError(
                                "snap.canonical.unreachable source={Source} after={Failures} retries; aborting follower ({Reason})",
                                canonical.Name, consecutiveSourceFailures, reason);
                            return new FollowerRunResult(
                                FollowerExitReason.SourceUnavailable,
                                lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                                SnapshotRestoreTarget: null,
                                Detail: $"canonical source {canonical.Name} unreachable after {consecutiveSourceFailures} consecutive polls ({reason})");
                        }
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Successful poll — reset the failure budget.
                    consecutiveSourceFailures = 0;

                    if (tip.BlockNumber <= lastCommittedBlock
                        || (lastSeenTipHash != null && tip.BlockHash != null && ByteUtil.AreEqual(tip.BlockHash, lastSeenTipHash) && tip.BlockNumber == lastSeenTipBlock))
                    {
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    // snap.canonical.forkchoice is now emitted exactly once
                    // per tip change at the canonical source itself (see
                    // LightClientCanonicalSource.GetLatestAsync). The
                    // duplicate emit here was load-bearing on the legacy path
                    // before that move; intentionally dropped to keep LOG-2
                    // single-sourced.

                    if (tip.BlockHash != null && tip.BlockHash.Length > 0)
                    {
                        try
                        {
                            var localHeader = await bundle.Blocks.GetByHashAsync(tip.BlockHash).ConfigureAwait(false);
                            if (localHeader == null)
                            {
                                logger.LogWarning(
                                    "snap.canonical.unknown_head hash={Hash} — fetching from P2P",
                                    tip.BlockHash.ToHex());
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex,
                                "snap.canonical.unknown_head_probe_failed hash={Hash}",
                                tip.BlockHash.ToHex());
                        }
                    }

                    lastSeenTipBlock = tip.BlockNumber;
                    lastSeenTipHash = tip.BlockHash;

                    ulong cursor = lastCommittedBlock;
                    ulong delta = tip.BlockNumber - cursor;
                    if (delta < options.WalkerInvocationThreshold)
                    {
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    logger.LogInformation(
                        "snap.cycle.restart reason=\"walker_invoked, delta={Delta}\"",
                        delta);

                    WalkerOutcome walkResult;
                    try
                    {
                        walkResult = await _walker(
                            fromBlockNumber: tip.BlockNumber,
                            fromHash: tip.BlockHash,
                            toBlockNumber: cursor,
                            bundle: bundle,
                            ct: ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "snap.walker.exception tip={Tip}; backing off and retrying",
                            tip.BlockNumber);
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    logger.LogInformation(
                        "snap.walker.exit reason={ExitReason} blocks_walked={BlocksWalked}",
                        walkResult.ExitReason, walkResult.HeadersWritten);

                    switch (walkResult.ExitReason)
                    {
                        case WalkerExitReason.LastKnownGoodDivergence:
                            if (_ancestorResolver == null || walkResult.DivergenceBlock == null)
                            {
                                logger.LogError(
                                    "snap.walker.divergence at block {Block}; no ancestor resolver wired — halting",
                                    walkResult.DivergenceBlock);
                                return new FollowerRunResult(
                                    FollowerExitReason.FatalVerdict,
                                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                                    SnapshotRestoreTarget: null,
                                    Detail: $"backward-walker reported divergence at block {walkResult.DivergenceBlock}; " +
                                            "findAncestor binary search not yet implemented — manual rewind required");
                            }

                            logger.LogWarning(
                                "snap.walker.divergence at block {Block}; invoking findAncestor",
                                walkResult.DivergenceBlock);

                            ulong divergedBlock = walkResult.DivergenceBlock.Value;
                            ulong floorBlock = bundle.Metadata.GetLastBlock();
                            if (floorBlock > divergedBlock) floorBlock = divergedBlock;

                            ulong ancestorBlock;
                            try
                            {
                                ancestorBlock = await _ancestorResolver(divergedBlock, floorBlock, ct)
                                    .ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception resolverEx)
                            {
                                logger.LogError(resolverEx,
                                    "snap.ancestor.resolver_failed diverged={Diverged} floor={Floor}; halting",
                                    divergedBlock, floorBlock);
                                return new FollowerRunResult(
                                    FollowerExitReason.FatalVerdict,
                                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                                    SnapshotRestoreTarget: null,
                                    Detail: $"ancestor resolver threw {resolverEx.GetType().Name}: {resolverEx.Message}");
                            }

                            var ancestorHash = await bundle.Blocks
                                .GetHashByNumberAsync(new BigInteger(ancestorBlock))
                                .ConfigureAwait(false);

                            using (var rewindBatch = bundle.BeginBatch())
                            {
                                rewindBatch.SetLastFetchedHeaderAndBody(ancestorBlock, ancestorBlock);
                                if (ancestorHash != null)
                                {
                                    rewindBatch.Commit(ancestorBlock, ancestorHash);
                                }
                                await rewindBatch.CommitAsync(ct).ConfigureAwait(false);
                            }

                            bundle.Metadata.DeleteCheckpointsAbove(ancestorBlock);

                            lastCommittedBlock = ancestorBlock;
                            lastCommittedHash = ancestorHash ?? lastCommittedHash;
                            lastSeenTipBlock = 0UL;
                            lastSeenTipHash = null;

                            logger.LogInformation(
                                "snap.ancestor.rewound block={Ancestor} diverged={Diverged}",
                                ancestorBlock, divergedBlock);
                            continue;

                        case WalkerExitReason.PeerPoolEmpty:
                            lastSeenTipBlock = 0UL;
                            lastSeenTipHash = null;
                            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                            continue;

                        case WalkerExitReason.MetExistingStore:
                        case WalkerExitReason.ReachedTarget:
                        case WalkerExitReason.StructuralGenesis:
                            break;

                        case WalkerExitReason.Cancelled:
                            throw new OperationCanceledException(ct);
                    }

                    ulong from = cursor + 1;
                    ulong to = tip.BlockNumber;
                    if (to >= from)
                    {
                        var forwardSource = new OrderingBlockSource(bundle, from, to);
                        var forwardResult = await ExecuteForwardAsync(
                            forwardSource, bundle, executor, options,
                            lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                            ct, logger).ConfigureAwait(false);
                        lastCommittedBlock = forwardResult.LastCommitted;
                        lastCommittedHash = forwardResult.LastCommittedHash ?? lastCommittedHash;
                        blocksExecuted = forwardResult.BlocksExecuted;
                        rootMismatches = forwardResult.RootMismatches;

                        if (forwardResult.Terminal is FollowerRunResult terminal)
                        {
                            return terminal;
                        }

                        if (forwardResult.Rewound)
                        {
                            executor = executorFactory(bundle);
                            lastSeenTipBlock = 0UL;
                            lastSeenTipHash = null;
                            continue;
                        }
                    }

                    if (options.EndBlock.HasValue && lastCommittedBlock >= options.EndBlock.Value)
                    {
                        return new FollowerRunResult(
                            FollowerExitReason.SourceCompleted,
                            lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                            SnapshotRestoreTarget: null,
                            Detail: $"reached EndBlock={options.EndBlock.Value:N0}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new FollowerRunResult(
                    FollowerExitReason.Cancelled,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: "cancelled");
            }
            finally
            {
                if (bundle != null && lastCommittedBlock > 0)
                {
                    try { bundle.Metadata.Commit(lastCommittedBlock, lastCommittedHash); }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex,
                            "FollowerService finally-block Commit({block}, {hash}) failed; metadata may be stale on next start.",
                            lastCommittedBlock,
                            lastCommittedHash != null ? lastCommittedHash.ToHex() : "(null)");
                    }
                }
            }
        }

        private readonly struct ForwardExecOutcome
        {
            public ForwardExecOutcome(ulong lastCommitted, byte[] lastCommittedHash,
                ulong blocksExecuted, ulong rootMismatches, FollowerRunResult terminal, bool rewound = false)
            {
                LastCommitted = lastCommitted;
                LastCommittedHash = lastCommittedHash;
                BlocksExecuted = blocksExecuted;
                RootMismatches = rootMismatches;
                Terminal = terminal;
                Rewound = rewound;
            }
            public ulong LastCommitted { get; }
            public byte[] LastCommittedHash { get; }
            public ulong BlocksExecuted { get; }
            public ulong RootMismatches { get; }
            public FollowerRunResult Terminal { get; }
            public bool Rewound { get; }
        }

        private async Task<ForwardExecOutcome> ExecuteForwardAsync(
            IBlockSource source,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            await foreach (var blockBundle in source.StreamAsync(lastCommittedBlock + 1, ct).ConfigureAwait(false))
            {
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
                    blocksExecuted++;
                    bundle.Metadata.Commit(lastCommittedBlock, lastCommittedHash);

                    if (options.CheckpointEvery > 0
                        && lastCommittedBlock > 0
                        && lastCommittedBlock % options.CheckpointEvery == 0)
                    {
                        try
                        {
                            await bundle.SaveCheckpointAsync(
                                lastCommittedBlock, result.ComputedStateRoot, lastCommittedHash, ct)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception cpEx)
                        {
                            logger.LogWarning(cpEx,
                                "Checkpoint save failed at block {block}; sync continues without this checkpoint.",
                                lastCommittedBlock);
                        }
                    }
                }
                else
                {
                    rootMismatches++;

                    var rewindOutcome = await TryRewindOnRootMismatchAsync(
                        bundle, lastCommittedBlock, blockNumber, options, _ancestorResolver, logger, ct).ConfigureAwait(false);

                    if (rewindOutcome.Terminal is FollowerRunResult terminalResult)
                    {
                        return new ForwardExecOutcome(
                            lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                            terminalResult);
                    }

                    return new ForwardExecOutcome(
                        rewindOutcome.NewHead,
                        rewindOutcome.NewHeadHash ?? lastCommittedHash,
                        blocksExecuted, rootMismatches,
                        terminal: null,
                        rewound: true);
                }
            }
            return new ForwardExecOutcome(lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches, null);
        }

        private readonly struct ForwardRewindOutcome
        {
            public ForwardRewindOutcome(ulong newHead, byte[] newHeadHash, FollowerRunResult terminal)
            {
                NewHead = newHead;
                NewHeadHash = newHeadHash;
                Terminal = terminal;
            }
            public ulong NewHead { get; }
            public byte[] NewHeadHash { get; }
            public FollowerRunResult Terminal { get; }
        }

        private static async Task<ForwardRewindOutcome> TryRewindOnRootMismatchAsync(
            IChainStoreBundle bundle,
            ulong lastCommittedBlock,
            ulong divergedBlock,
            FollowerOptions options,
            AncestorResolverDelegate ancestorResolver,
            ILogger logger,
            CancellationToken ct)
        {
            if (lastCommittedBlock == 0)
            {
                return new ForwardRewindOutcome(
                    lastCommittedBlock, null,
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"tip-driven forward exec: state-root mismatch at block {divergedBlock:N0}; no rewind target above genesis"));
            }

            ulong target = lastCommittedBlock > 0 ? lastCommittedBlock - 1 : 0;

            // Deep-reorg recovery: when a resolver is wired, binary-search
            // upstream for the last common ancestor within the configured
            // MaxReorgDepth window, then rewind to that ancestor in one step
            // instead of the linear single-block walk. Failure of the resolver
            // (network, no peers, etc.) falls through to the previous
            // single-step behaviour so a transient peer outage doesn't
            // escalate to FatalVerdict.
            if (ancestorResolver != null && lastCommittedBlock > 0)
            {
                ulong floor = lastCommittedBlock > options.MaxReorgDepth
                    ? lastCommittedBlock - options.MaxReorgDepth
                    : 0UL;
                try
                {
                    var ancestor = await ancestorResolver(divergedBlock, floor, ct).ConfigureAwait(false);
                    if (ancestor < lastCommittedBlock)
                    {
                        target = ancestor;
                        logger.LogWarning(
                            "snap.tip.root_mismatch block={Block}; ancestor_resolver target={Target} (floor={Floor})",
                            divergedBlock, target, floor);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "snap.tip.ancestor_resolver_failed block={Block}; falling back to single-step rewind",
                        divergedBlock);
                }
            }

            logger.LogWarning(
                "snap.tip.root_mismatch block={Block}; attempting rewind to {Target}",
                divergedBlock, target);

            var coordinator = new RewindCoordinator(bundle);
            RewindResult rewindResult;
            try
            {
                rewindResult = await coordinator
                    .RewindToAsync(target, RewindPolicy.JournalFirstThenSnapshot, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "snap.tip.rewind_failed block={Block} target={Target}",
                    divergedBlock, target);
                return new ForwardRewindOutcome(
                    lastCommittedBlock, null,
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"tip-driven forward exec: rewind threw {ex.GetType().Name}: {ex.Message}"));
            }

            switch (rewindResult.Outcome)
            {
                case RewindOutcome.JournalUsed:
                    var newHead = bundle.Metadata.GetLastBlock();
                    var newHeadHash = bundle.Metadata.GetLastBlockHash();
                    logger.LogInformation(
                        "snap.tip.rewound new_head={Head} undone={Undone}",
                        newHead, rewindResult.UndoneCount);
                    return new ForwardRewindOutcome(newHead, newHeadHash, terminal: null);

                case RewindOutcome.SnapshotUsed:
                    return new ForwardRewindOutcome(
                        lastCommittedBlock, null,
                        new FollowerRunResult(
                            FollowerExitReason.SnapshotRestoreRequested,
                            lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 1,
                            SnapshotRestoreTarget: rewindResult.RestoredCheckpoint,
                            Detail: rewindResult.Detail));

                case RewindOutcome.NoOp:
                case RewindOutcome.NoPathAvailable:
                default:
                    return new ForwardRewindOutcome(
                        lastCommittedBlock, null,
                        new FollowerRunResult(
                            FollowerExitReason.RewindUnavailable,
                            lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                            SnapshotRestoreTarget: null,
                            Detail: $"tip-driven forward exec: rewind unavailable at block {divergedBlock:N0}: {rewindResult.Detail}"));
            }
        }

        /// <summary>
        /// In-process <see cref="IBlockSource"/> adapter over an
        /// <see cref="IChainStoreBundle"/>: reads blocks ascending from the
        /// inclusive range [from, to] using whatever the bundle has on disk.
        /// Used by the tip-poll loop after the backward walker has filled the
        /// skeleton — the forward executor then sees the new range as a
        /// normal block stream without knowing it came from local storage.
        /// </summary>
        internal sealed class OrderingBlockSource : IBlockSource
        {
            private readonly IChainStoreBundle _bundle;
            private readonly ulong _from;
            private readonly ulong _to;

            public OrderingBlockSource(IChainStoreBundle bundle, ulong fromInclusive, ulong toInclusive)
            {
                _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
                _from = fromInclusive;
                _to = toInclusive;
            }

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [EnumeratorCancellation] CancellationToken ct)
            {
                ulong start = fromBlock > _from ? fromBlock : _from;
                for (ulong n = start; n <= _to; n++)
                {
                    ct.ThrowIfCancellationRequested();
                    var header = await _bundle.Blocks.GetByNumberAsync(n).ConfigureAwait(false);
                    if (header == null) yield break;
                    var hash = await _bundle.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                    if (hash == null) yield break;
                    var uncles = await _bundle.Uncles.GetByBlockHashAsync(hash).ConfigureAwait(false);
                    var txs = await _bundle.Transactions.GetByBlockHashAsync(hash).ConfigureAwait(false);
                    var withdrawals = await _bundle.Withdrawals.GetByBlockHashAsync(hash).ConfigureAwait(false);
                    yield return new BlockBundle(
                        Header: header,
                        Transactions: txs ?? new List<ISignedTransaction>(),
                        Uncles: uncles ?? new List<BlockHeader>(),
                        Withdrawals: withdrawals,
                        HeaderHash: hash);
                }
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal LastChainBreak => null;
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
