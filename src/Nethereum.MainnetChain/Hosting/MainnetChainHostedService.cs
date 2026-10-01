using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.MainnetChain.Configuration;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class MainnetChainHostedService : BackgroundService
    {
        private readonly MainnetChainNodeFactory _nodeFactory;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockSource _source;
        private readonly IValidationPolicy _policy;
        private readonly ICanonicalStateRootSource? _canonical;
        private readonly MainnetChainServerConfig _config;
        private readonly ChainNodeConfig? _mappedConfig;
        private readonly ILogger<MainnetChainHostedService> _logger;
        private readonly IPeerPool? _pool;
        private readonly IFetchRequestScheduler? _scheduler;
        private readonly ITransactionSubmissionService? _txSubmission;
        private readonly MainnetChainNodeAccessor _nodeAccessor;
        private readonly SnapSyncMetrics? _metrics;
        private readonly HeaderFollowService? _headerFollow;
        private readonly IHostApplicationLifetime? _lifetime;
        private FollowerRunResult? _lastResult;

        public const string RestoreRequestFileName = BootRecoveryGate.RestoreRequestFileName;

        private readonly BootRecoveryGate _recoveryGate;

        public MainnetChainHostedService(
            MainnetChainNodeFactory nodeFactory,
            IChainStoreBundle bundle,
            IBlockSource source,
            IValidationPolicy policy,
            MainnetChainServerConfig config,
            ILogger<MainnetChainHostedService> logger,
            MainnetChainNodeAccessor nodeAccessor,
            ICanonicalStateRootSource? canonical = null,
            IPeerPool? pool = null,
            IFetchRequestScheduler? scheduler = null,
            SnapSyncMetrics? metrics = null,
            HeaderFollowService? headerFollow = null,
            IHostApplicationLifetime? lifetime = null,
            ITransactionSubmissionService? txSubmission = null,
            ChainNodeConfig? mappedConfig = null)
        {
            _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _recoveryGate = new BootRecoveryGate(_logger);
            _nodeAccessor = nodeAccessor ?? throw new ArgumentNullException(nameof(nodeAccessor));
            _canonical = canonical;
            _pool = pool;
            _scheduler = scheduler;
            _metrics = metrics;
            _headerFollow = headerFollow;
            _lifetime = lifetime;
            _txSubmission = txSubmission;
            _mappedConfig = mappedConfig;
        }

        public FollowerRunResult? LastResult => _lastResult;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "MainnetChain follower starting (start_block={Start}, blocks={Blocks}, data_dir={DataDir})",
                _config.StartBlock,
                _config.Blocks,
                _config.DataDir ?? "<in-memory>");

            try
            {
                if (_config.VerifyFlat)
                {
                    await RunVerifyFlatAsync(stoppingToken).ConfigureAwait(false);
                    _lifetime?.StopApplication();
                    return;
                }

                await WipeSnapBootstrapStateIfRequestedAsync(stoppingToken).ConfigureAwait(false);

                await RunCompactAllIfRequestedAsync(stoppingToken).ConfigureAwait(false);

                await RunRebuildStateFromFlatIfRequestedAsync(stoppingToken).ConfigureAwait(false);

                await CompactStateAtBootIfPressuredAsync(stoppingToken).ConfigureAwait(false);

                Task headerFollowTask = LaunchHeaderFollowJob(stoppingToken);

                var node = BuildFollowerNode();
                _nodeAccessor.Set(node);

                var snapResult = await SnapBootstrapInvoker.RunIfConfiguredAsync(
                    _bundle, _pool, _scheduler, _config, _logger, stoppingToken, _canonical, _metrics,
                    headerFollow: _headerFollow).ConfigureAwait(false);
                if (!snapResult.Ran && !string.IsNullOrEmpty(snapResult.SkipReason))
                {
                    _logger.LogInformation("Snap-bootstrap: not run — {Reason}.", snapResult.SkipReason);
                }

                var historyBackfillTask = snapResult.HistoryBackfill ?? Task.CompletedTask;
                if (!historyBackfillTask.IsCompleted)
                    _logger.LogInformation("History backfill: draining pre-pivot archive in background (cursor={Cursor})",
                        _bundle.Metadata.GetLastFetchedBody());

                if (_headerFollow == null)
                {
                    if (!historyBackfillTask.IsCompleted)
                    {
                        _logger.LogInformation("History backfill: draining inline (legacy mode has no background archive owner)");
                        await historyBackfillTask.ConfigureAwait(false);
                    }
                    var lastBlock = _bundle.Metadata.GetLastBlock();
                    var bodyCursor = _bundle.Metadata.GetLastFetchedBody();
                    if (lastBlock > 0 && bodyCursor < lastBlock)
                        _logger.LogWarning(
                            "Pre-pivot archive incomplete (bodies at {Cursor} of {Pivot}) and legacy mode has no background owner — " +
                            "bodies in that range stay missing until a body follower runs or the node is restarted with the Headers job enabled.",
                            bodyCursor, lastBlock);
                }

                Task tipBandFollowTask = LaunchTipBandBodyFollow(stoppingToken);

                Task receiptBackfillTask = LaunchReceiptBackfillScrub(stoppingToken);

                Task historyBackfillFollowerTask = LaunchHistoryBackfill(stoppingToken);

                LaunchFreezeIndexing();

                await RunSupervisedFollowerLoopAsync(node, stoppingToken).ConfigureAwait(false);

                await DrainBackgroundJobsAsync(
                    historyBackfillTask, snapResult, receiptBackfillTask, tipBandFollowTask, headerFollowTask,
                    historyBackfillFollowerTask, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MainnetChain follower cancelled.");
            }
        }

        private async Task RunVerifyFlatAsync(CancellationToken stoppingToken)
        {
            var headBlock = _bundle.Metadata.GetLastBlock();
            if (headBlock == 0)
            {
                _logger.LogWarning("VerifyFlat set but no committed state (LastBlock=0) — nothing to verify; stopping.");
                return;
            }

            var headHeader = await _bundle.Blocks.GetByNumberAsync(headBlock).ConfigureAwait(false);
            if (headHeader?.StateRoot == null || headHeader.StateRoot.Length != 32)
                throw new InvalidOperationException(
                    $"VerifyFlat: committed head {headBlock} has no header state root in the store — cannot verify. Aborting.");
            var expectedRoot = headHeader.StateRoot;

            if (!_bundle.StateTrieNodes.ContainsKey(expectedRoot))
                throw new InvalidOperationException(
                    $"VerifyFlat: the committed head {headBlock} account root 0x{expectedRoot.ToHex()} " +
                    "is not present in the trie store — nothing to verify flat against. Refusing.");

            if (_bundle is not IFlatStateReconciler reconciler)
                throw new InvalidOperationException(
                    "VerifyFlat: the store does not expose IFlatStateReconciler — cannot verify flat against trie.");

            var sampleCap = _config.VerifyFlatSampleAccountsPerShard;
            _logger.LogWarning(
                "VerifyFlat set — write-free verify of FLAT against the canonical TRIE at committed head {Head} " +
                "(0x{Root}); sample={Sample}. One-shot diagnostic; the host stops after this pass.",
                headBlock, expectedRoot.ToHex(), sampleCap > 0 ? sampleCap.ToString() : "full");
            var verifySw = System.Diagnostics.Stopwatch.StartNew();
            var verify = await reconciler.VerifyFlatStateAsync(
                    expectedRoot,
                    msg => _logger.LogInformation("state.verify {Progress} elapsed={Elapsed}", msg, verifySw.Elapsed),
                    stoppingToken,
                    sampleCap)
                .ConfigureAwait(false);
            _logger.LogWarning(
                "VerifyFlat done in {Elapsed} at head {Head} (0x{Root}): " +
                "accounts scanned={AccountsScanned} patched={AccountsPatched} added={AccountsAdded} ghosts={AccountGhosts}; " +
                "slots scanned={SlotsScanned} patched={SlotsPatched} added={SlotsAdded} ghosts={SlotGhosts}; " +
                "total={Total}; sample={Sample}.",
                verifySw.Elapsed, headBlock, expectedRoot.ToHex(),
                verify.AccountsScanned, verify.AccountsPatched, verify.AccountsAdded, verify.GhostAccountsDeleted,
                verify.SlotsScanned, verify.SlotsPatched, verify.SlotsAdded, verify.GhostSlotsDeleted,
                verify.TotalRepairs, sampleCap > 0 ? sampleCap.ToString() : "full");
        }

        private async Task RunCompactAllIfRequestedAsync(CancellationToken stoppingToken)
        {
            if (_config.CompactAll && _bundle is IStateCompaction fullCompaction)
            {
                _logger.LogWarning("CompactAll set — running a full store compaction before sync; this can take a while.");
                await fullCompaction.CompactAllAsync(
                    msg => _logger.LogInformation("compact.all {Progress}", msg), stoppingToken).ConfigureAwait(false);
                _logger.LogWarning("CompactAll complete.");
            }
        }

        private async Task RunRebuildStateFromFlatIfRequestedAsync(CancellationToken stoppingToken)
        {
            if (_config.RebuildStateFromFlat)
            {
                var headBlock = _bundle.Metadata.GetLastBlock();
                if (headBlock == 0)
                {
                    _logger.LogWarning("RebuildStateFromFlat set but no committed state (LastBlock=0) — nothing to repair; skipping.");
                }
                else
                {
                    var headHeader = await _bundle.Blocks.GetByNumberAsync(headBlock).ConfigureAwait(false);
                    if (headHeader?.StateRoot == null || headHeader.StateRoot.Length != 32)
                        throw new InvalidOperationException(
                            $"RebuildStateFromFlat: committed head {headBlock} has no header state root in the store — cannot verify the repair. Aborting.");
                    var expectedRoot = headHeader.StateRoot;

                    if (!_bundle.StateTrieNodes.ContainsKey(expectedRoot))
                        throw new InvalidOperationException(
                            $"RebuildStateFromFlat: the committed head {headBlock} account root 0x{expectedRoot.ToHex()} " +
                            "is not present in the trie store — nothing to reconcile flat against. Did you also set " +
                            "WipeState? Run a state resync instead. Refusing.");

                    if (_bundle is not IFlatStateReconciler reconciler)
                        throw new InvalidOperationException(
                            "RebuildStateFromFlat: the store does not expose IFlatStateReconciler — cannot reconcile flat to trie.");

                    _logger.LogWarning(
                        "RebuildStateFromFlat set — reconciling FLAT to the canonical TRIE at committed head {Head} " +
                        "(0x{Root}); the trie is the source of truth. One-shot full-state pass; clear the flag after.",
                        headBlock, expectedRoot.ToHex());
                    var reconcileSw = System.Diagnostics.Stopwatch.StartNew();
                    var reconcile = await reconciler.ReconcileFlatStateAsync(
                            expectedRoot,
                            msg => _logger.LogInformation("state.reconcile {Progress} elapsed={Elapsed}", msg, reconcileSw.Elapsed),
                            stoppingToken)
                        .ConfigureAwait(false);
                    _logger.LogWarning(
                        "RebuildStateFromFlat reconcile done in {Elapsed}: accounts patched={AP} added={AA} ghosts={AG}, " +
                        "slots patched={SP} added={SA} ghosts={SG}.",
                        reconcileSw.Elapsed, reconcile.AccountsPatched, reconcile.AccountsAdded, reconcile.GhostAccountsDeleted,
                        reconcile.SlotsPatched, reconcile.SlotsAdded, reconcile.GhostSlotsDeleted);

                    var verifySw = System.Diagnostics.Stopwatch.StartNew();
                    var verify = await reconciler.VerifyFlatStateAsync(
                            expectedRoot,
                            msg => _logger.LogInformation("state.reconcile.verify {Progress} elapsed={Elapsed}", msg, verifySw.Elapsed),
                            stoppingToken)
                        .ConfigureAwait(false);
                    if (verify.TotalRepairs != 0)
                        throw new InvalidOperationException(
                            $"RebuildStateFromFlat: post-reconcile verify still found {verify.TotalRepairs} flat/trie diffs at head " +
                            $"{headBlock} (0x{expectedRoot.ToHex()}) — refusing to go live.");
                    _logger.LogWarning(
                        "RebuildStateFromFlat VERIFIED in {Elapsed}: flat == trie at head {Head} (0x{Root}), zero diffs. " +
                        "Clear the flag before the next restart.",
                        verifySw.Elapsed, headBlock, expectedRoot.ToHex());
                }
            }
        }

        private FollowerChainNode BuildFollowerNode()
        {
            var bounds = EffectiveStartBlockResolver.Resolve(
                _bundle.Metadata.GetSnapSyncState(),
                _bundle.Metadata.GetLastBlock(),
                _config);
            var options = EffectiveStartBlockResolver.BuildOptions(bounds, _config);
            if (_headerFollow != null)
                options = options with { ExternalHeaderFollow = true };
            var node = _nodeFactory.Build(_bundle, _source, _policy, options, _canonical);
            var rpcCaps = ResolveRpcCaps(_mappedConfig, _config);
            node.ChainConfig.RpcMaxLogBlockRange = rpcCaps.MaxLogBlockRange;
            node.ChainConfig.RpcMaxLogResults = rpcCaps.MaxLogResults;
            node.ChainConfig.RpcGasCap = rpcCaps.GasCap;
            if (_txSubmission != null)
                node.TransactionSubmission = _txSubmission;
            return node;
        }

        public static (int MaxLogBlockRange, int MaxLogResults, long GasCap) ResolveRpcCaps(
            ChainNodeConfig? mappedConfig, MainnetChainServerConfig config) =>
            (
                mappedConfig?.Rpc.MaxLogBlockRange ?? config.RpcMaxLogBlockRange,
                mappedConfig?.Rpc.MaxLogResults ?? config.RpcMaxLogResults,
                mappedConfig?.Rpc.GasCap ?? config.RpcGasCap
            );

        private async Task RunSupervisedFollowerLoopAsync(FollowerChainNode node, CancellationToken stoppingToken)
        {
            var supervisionBackoff = TimeSpan.FromSeconds(15);
            var maxSupervisionBackoff = TimeSpan.FromMinutes(5);
            var keepFollowing = true;
            while (keepFollowing && !stoppingToken.IsCancellationRequested)
            {
                var bounds = EffectiveStartBlockResolver.Resolve(
                    _bundle.Metadata.GetSnapSyncState(),
                    _bundle.Metadata.GetLastBlock(),
                    _config);

                if (bounds.Reason == EffectiveStartBlockResolver.StartBlockReason.PostSnapPivotFastStart)
                {
                    _logger.LogInformation(
                        "Post-snap startup: StartBlock={EffectiveStart} (pivot={Pivot}, EndBlock={EndBlock})",
                        bounds.StartBlock,
                        bounds.StartBlock - 1,
                        bounds.EndBlock?.ToString() ?? "<unbounded>");
                }
                else if (bounds.Reason == EffectiveStartBlockResolver.StartBlockReason.ResumeFromLastBlock)
                {
                    _logger.LogInformation(
                        "Resume startup: StartBlock={EffectiveStart} (last_committed={LastBlock}, EndBlock={EndBlock})",
                        bounds.StartBlock,
                        bounds.StartBlock - 1,
                        bounds.EndBlock?.ToString() ?? "<unbounded>");
                }

                var options = EffectiveStartBlockResolver.BuildOptions(bounds, _config);
                if (_headerFollow != null)
                    options = options with { ExternalHeaderFollow = true };

                _lastResult = await node.RunAsync(options, stoppingToken, _logger).ConfigureAwait(false);
                _logger.LogInformation(
                    "MainnetChain follower exited: reason={Reason}, last_block={Last}, executed={Executed}, root_mismatches={Mismatches}",
                    _lastResult.ExitReason,
                    _lastResult.LastExecutedBlock,
                    _lastResult.BlocksExecuted,
                    _lastResult.RootMismatches);

                if (_lastResult.BlocksExecuted > 0)
                    supervisionBackoff = TimeSpan.FromSeconds(15);

                var decision = DecideFollowerSupervision(
                    _lastResult.ExitReason, supervisionBackoff, maxSupervisionBackoff);
                keepFollowing = decision.KeepFollowing;

                switch (_lastResult.ExitReason)
                {
                    case FollowerExitReason.Cancelled:
                    case FollowerExitReason.SourceCompleted:
                        break;

                    case FollowerExitReason.SourceUnavailable:
                        _logger.LogWarning(
                            "follower.supervision source unavailable — retrying in {Backoff} (the canonical source may be down; the follower resumes from its committed cursor)",
                            supervisionBackoff);
                        try { await Task.Delay(supervisionBackoff, stoppingToken).ConfigureAwait(false); }
                        catch (OperationCanceledException) { keepFollowing = false; break; }
                        supervisionBackoff = decision.NextBackoff;
                        break;

                    case FollowerExitReason.SnapshotRestoreRequested:
                        var target = _lastResult.SnapshotRestoreTarget;
                        if (target.HasValue)
                        {
                            _recoveryGate.RecordRestoreRequest(_config.DataDir, target.Value.BlockNumber);
                            _logger.LogCritical(
                                "follower.supervision snapshot restore REQUIRED to checkpoint block {Block} — " +
                                "recorded in {Marker}; RESTART THE PROCESS to apply it (the restore swaps the data dir and needs the store closed). " +
                                "RPC keeps serving the pre-restore state until then.",
                                target.Value.BlockNumber, RestoreRequestFileName);
                        }
                        else
                        {
                            _logger.LogCritical(
                                "follower.supervision snapshot restore requested but no checkpoint target was reported — manual recovery required. Detail: {Detail}",
                                _lastResult.Detail);
                        }
                        break;

                    case FollowerExitReason.FatalVerdict:
                    case FollowerExitReason.RewindUnavailable:
                    default:
                        _logger.LogCritical(
                            "follower.supervision HALTED ({Reason}) — execution diverged or cannot recover without an operator; " +
                            "RPC keeps serving the last committed state. Detail: {Detail}",
                            _lastResult.ExitReason, _lastResult.Detail);
                        break;
                }
            }
        }

        public static (bool KeepFollowing, TimeSpan NextBackoff) DecideFollowerSupervision(
            FollowerExitReason reason, TimeSpan currentBackoff, TimeSpan maxBackoff)
        {
            switch (reason)
            {
                case FollowerExitReason.SourceUnavailable:
                    return (true, TimeSpan.FromTicks(
                        Math.Min(currentBackoff.Ticks * 2, maxBackoff.Ticks)));

                case FollowerExitReason.Cancelled:
                case FollowerExitReason.SourceCompleted:
                case FollowerExitReason.SnapshotRestoreRequested:
                case FollowerExitReason.FatalVerdict:
                case FollowerExitReason.RewindUnavailable:
                default:
                    return (false, currentBackoff);
            }
        }

        private async Task WipeSnapBootstrapStateIfRequestedAsync(CancellationToken stoppingToken)
        {
            if (_config.WipeState || string.Equals(Environment.GetEnvironmentVariable("NETHEREUM_WIPE_SNAP_STATE"), "1", StringComparison.Ordinal))
            {
                _logger.LogWarning("WipeState set — wiping state / trie / state-history CFs and SnapSyncState metadata; receipts, logs and Phase 1 cursors are preserved.");
                await _bundle.ResetSnapBootstrapStateAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogWarning("Snap-bootstrap state wipe complete; Phase 1 archive untouched. Snap bootstrap will re-stream from a fresh pivot.");
            }
        }

        private async Task CompactStateAtBootIfPressuredAsync(CancellationToken stoppingToken)
        {
            if (_bundle is IStateCompaction bootCompaction
                && _bundle is IStateWriteBackpressure bootValve
                && bootValve.ShouldPauseStateWrites())
            {
                _logger.LogWarning(
                    "Boot-time state compaction: the state write valve reports pressure ({Pressure}) — " +
                    "collapsing it deliberately before the sync pipelines start.",
                    bootValve.DescribeStateBackpressure());
                var bootCompactSw = System.Diagnostics.Stopwatch.StartNew();
                await bootCompaction.CompactStateAsync(
                        msg => _logger.LogInformation("snap.state.compact {Progress}", msg), stoppingToken)
                    .ConfigureAwait(false);
                _logger.LogInformation(
                    "Boot-time state compaction done in {Elapsed}; valve now reports: {Pressure}",
                    bootCompactSw.Elapsed, bootValve.DescribeStateBackpressure());
            }
        }

        private Task LaunchHeaderFollowJob(CancellationToken stoppingToken)
        {
            Task headerFollowTask = Task.CompletedTask;
            if (_headerFollow != null)
            {
                headerFollowTask = Task.Run(async () =>
                {
                    var backoff = TimeSpan.FromSeconds(5);
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _headerFollow.RunAsync(_bundle, stoppingToken).ConfigureAwait(false);
                            return;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Headers job crashed; restarting in {Backoff}", backoff);
                            try { await Task.Delay(backoff, stoppingToken).ConfigureAwait(false); }
                            catch (OperationCanceledException) { return; }
                            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, TimeSpan.FromMinutes(2).Ticks));
                        }
                    }
                }, stoppingToken);
                _logger.LogInformation("Headers job: launched (single header source; skeleton follows the canonical tip)");
            }
            return headerFollowTask;
        }

        private Task LaunchTipBandBodyFollow(CancellationToken stoppingToken)
        {
            Task tipBandFollowTask = Task.CompletedTask;
            if (_headerFollow != null && _scheduler != null && _pool != null)
            {
                var tipBandFollow = new TipBandBodyFollowService(
                    _scheduler, _pool, _bundle, MainnetChainActivations.Instance, _logger);
                tipBandFollowTask = Task.Run(
                    () => tipBandFollow.RunAsync(stoppingToken), stoppingToken);
                _logger.LogInformation(
                    "Tip-band body follow: enabled from startup (execution head → trusted tip), independent of the history drain");
            }
            return tipBandFollowTask;
        }

        private Task LaunchReceiptBackfillScrub(CancellationToken stoppingToken)
        {
            Task receiptBackfillTask = Task.CompletedTask;
            if (_config.ReceiptBackfill && _scheduler != null)
            {
                var backfill = new ReceiptBackfillService(
                    _bundle,
                    _scheduler,
                    logger: _logger);
                receiptBackfillTask = Task.Run(
                    () => backfill.RunAsync(stoppingToken), stoppingToken);
                _logger.LogInformation(
                    "Receipt-backfill scrub: enabled (cursor={Cursor})",
                    _bundle.Metadata.GetReceiptBackfillCursor());
            }
            return receiptBackfillTask;
        }

        private void LaunchFreezeIndexing()
        {
            if (_config.BackgroundFreezeIndexing)
                _logger.LogInformation(
                    "Freeze indexing: starting the background trailer behind the follower (deferred out of state sync).");
            (_bundle as IBulkDurabilityBoundary)?.StartBackgroundFreezeIndexing();
        }

        private Task LaunchHistoryBackfill(CancellationToken stoppingToken)
        {
            var pivot = _bundle.Metadata.GetLastBlock();
            var cursor = _bundle.Metadata.GetLastFetchedBody();
            var decision = DeferredHistoryBackfillPlanner.Decide(
                _config.RunHistoryBackfillAfterStateSync, _scheduler != null && _pool != null, _headerFollow != null, pivot, cursor);
            switch (decision)
            {
                case DeferredHistoryBackfillDecision.NotDeferred:
                    return Task.CompletedTask;
                case DeferredHistoryBackfillDecision.PeeringUnavailable:
                    _logger.LogWarning(
                        "History backfill (after-state-sync) requested but no peer scheduler is available; history will not be filled.");
                    return Task.CompletedTask;
                case DeferredHistoryBackfillDecision.HeaderSkeletonUnavailable:
                    _logger.LogWarning(
                        "History backfill (after-state-sync) requested but the Headers follow job is not active (no beacon header source); " +
                        "the deferred backfill has nothing to lay the block-header skeleton, so history will NOT be filled. " +
                        "Use --history-backfill during, or start the node with a beacon header source.");
                    return Task.CompletedTask;
                case DeferredHistoryBackfillDecision.NothingToFill:
                    _logger.LogInformation(
                        "History backfill (after-state-sync): nothing to fill (cursor={Cursor}, pivot={Pivot})", cursor, pivot);
                    return Task.CompletedTask;
                case DeferredHistoryBackfillDecision.Run:
                    break;
                default:
                    return Task.CompletedTask;
            }

            _logger.LogInformation(
                "History backfill (after-state-sync): filling bodies+receipts behind the follower (cursor={Cursor} -> pivot={Pivot})",
                cursor, pivot);
            return Task.Run(async () =>
            {
                var backfiller = new ParallelBlockBackfiller(
                    _scheduler, _pool, new Nethereum.DevP2P.Sync.Scheduling.PeerRequestWorker(), _bundle,
                    rootsProvider: null, logger: _logger, activations: MainnetChainActivations.Instance, role: "history");
                var result = await backfiller.BackfillAsync(0, pivot, headersFromStore: true, stoppingToken).ConfigureAwait(false);
                if (result.Ran)
                    _logger.LogInformation(
                        "History backfill (after-state-sync) complete: {Blocks} blocks, {Txs} txs, {Rcpts} receipts up to block {End}.",
                        result.BlocksWritten, result.TransactionsWritten, result.ReceiptsWritten, result.EndBlock);
                (_bundle as IBulkDurabilityBoundary)?.CheckpointBulk();
            }, stoppingToken);
        }

        private async Task DrainBackgroundJobsAsync(
            Task historyBackfillTask,
            SnapBootstrapper.Result snapResult,
            Task receiptBackfillTask,
            Task tipBandFollowTask,
            Task headerFollowTask,
            Task historyBackfillFollowerTask,
            CancellationToken stoppingToken)
        {
            try { await historyBackfillTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "History backfill exited with error"); }
            try { await historyBackfillFollowerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "History backfill (after-state-sync) exited with error"); }
            try { await (snapResult.StateCompaction ?? Task.CompletedTask).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Background state compaction exited with error"); }
            try { await receiptBackfillTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Receipt-backfill scrub exited with error"); }
            try { await tipBandFollowTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Tip-band body follow exited with error"); }
            try { await headerFollowTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Headers job exited with error"); }
        }
    }
}
