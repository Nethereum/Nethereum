using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
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
        private readonly TipBandBodyFollowService? _tipBand;
        private readonly AsyncGate? _writeGate;
        private FollowerRunResult? _lastResult;
        private volatile InvalidOperationException? _storeFatal;

        private static readonly TimeSpan StoreFaultPollInterval = TimeSpan.FromSeconds(5);

        public const string RestoreRequestFileName = BootRecoveryGate.RestoreRequestFileName;

        private readonly BootRecoveryGate _recoveryGate;

        [Obsolete("The host application lifetime is no longer used.")]
        public MainnetChainHostedService(
            MainnetChainNodeFactory nodeFactory,
            IChainStoreBundle bundle,
            IBlockSource source,
            IValidationPolicy policy,
            MainnetChainServerConfig config,
            ILogger<MainnetChainHostedService> logger,
            MainnetChainNodeAccessor nodeAccessor,
            ICanonicalStateRootSource? canonical,
            IPeerPool? pool,
            IFetchRequestScheduler? scheduler,
            SnapSyncMetrics? metrics,
            HeaderFollowService? headerFollow,
            IHostApplicationLifetime? lifetime,
            ITransactionSubmissionService? txSubmission = null,
            ChainNodeConfig? mappedConfig = null)
            : this(nodeFactory, bundle, source, policy, config, logger, nodeAccessor, canonical, pool, scheduler, metrics, headerFollow, txSubmission, mappedConfig)
        {
        }

        [ActivatorUtilitiesConstructor]
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
            ITransactionSubmissionService? txSubmission = null,
            ChainNodeConfig? mappedConfig = null,
            TipBandBodyFollowService? tipBand = null,
            AsyncGate? writeGate = null)
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
            _txSubmission = txSubmission;
            _mappedConfig = mappedConfig;
            _tipBand = tipBand;
            _writeGate = writeGate;
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
                await RunFollowerStackAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MainnetChain follower cancelled.");
            }
            catch (Exception) when (_storeFatal != null)
            {
            }
            catch (Exception ex)
            {
                ReportFatal(ex);
                throw;
            }

            if (_storeFatal != null) throw _storeFatal;
        }

        private async Task RunFollowerStackAsync(CancellationToken stoppingToken)
        {
            await CompactStateAtBootIfPressuredAsync(stoppingToken).ConfigureAwait(false);

            using var jobsCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(jobsCts.Token);
            var jobs = new BackgroundJobs();
            var followerReturned = false;
            FollowerChainNode? node = null;
            try
            {
                jobs.HeaderFollow = LaunchHeaderFollowJob(jobsCts.Token);
                jobs.StoreFaultWatch = LaunchStoreFaultWatch(jobsCts, watchCts.Token);

                node = BuildFollowerNode();
                _nodeAccessor.Set(node);

                await BootstrapAndLaunchJobsAsync(jobs, jobsCts.Token).ConfigureAwait(false);

                await RunSupervisedFollowerLoopAsync(node, jobsCts.Token).ConfigureAwait(false);
                followerReturned = true;
            }
            finally
            {
                (_bundle as IBackfillPauseControl)?.SetYieldToExecution(false);
                if (!followerReturned) jobsCts.Cancel();
                watchCts.Cancel();
                await DrainBackgroundJobsAsync(jobs, jobsCts.Token).ConfigureAwait(false);
                node?.ReleaseExecutionResources();
            }
        }

        private async Task BootstrapAndLaunchJobsAsync(BackgroundJobs jobs, CancellationToken jobsToken)
        {
            if (_config.EngineOnly) return;

            var snapResult = await SnapBootstrapInvoker.RunIfConfiguredAsync(
                _bundle, _pool, _scheduler, _config, _logger, jobsToken, _canonical, _metrics,
                headerFollow: _headerFollow).ConfigureAwait(false);
            if (!snapResult.Ran && !string.IsNullOrEmpty(snapResult.SkipReason))
            {
                _logger.LogInformation("Snap-bootstrap: not run — {Reason}.", snapResult.SkipReason);
            }

            jobs.SnapHistory = snapResult.HistoryBackfill ?? Task.CompletedTask;
            jobs.StateCompaction = snapResult.StateCompaction ?? Task.CompletedTask;
            if (!jobs.SnapHistory.IsCompleted)
                _logger.LogInformation("History backfill: draining pre-pivot archive in background (cursor={Cursor})",
                    _bundle.Metadata.GetLastFetchedBody());

            if (_headerFollow == null)
                await DrainHistoryInlineWithoutHeaderFollowAsync(jobs.SnapHistory).ConfigureAwait(false);
            else
                (_bundle as IBackfillPauseControl)?.SetYieldToExecution(true);

            jobs.TipBand = LaunchTipBandBodyFollow(jobsToken);
            jobs.ReceiptScrub = LaunchReceiptBackfillScrub(jobsToken);
            jobs.HistoryFollower = LaunchHistoryBackfill(jobsToken);
            LaunchFreezeIndexing();
        }

        private async Task DrainHistoryInlineWithoutHeaderFollowAsync(Task historyBackfillTask)
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

        private void ReportFatal(Exception ex)
        {
            try
            {
                Environment.ExitCode = 1;
                string lastBlock;
                try { lastBlock = _bundle.Metadata.GetLastBlock().ToString(); }
                catch { lastBlock = "unknown"; }
                _logger.LogCritical(ex,
                    "follower.fatal MainnetChain follower stopped by an unhandled {ExceptionType}: {Message} — the host stops and the process exits with code 1 so the supervisor restarts it (last executed block {LastBlock})",
                    ex.GetType().Name, ex.Message, lastBlock);
            }
            catch
            {
            }
        }

        private sealed class BackgroundJobs
        {
            public Task HeaderFollow = Task.CompletedTask;
            public Task SnapHistory = Task.CompletedTask;
            public Task StateCompaction = Task.CompletedTask;
            public Task TipBand = Task.CompletedTask;
            public Task ReceiptScrub = Task.CompletedTask;
            public Task HistoryFollower = Task.CompletedTask;
            public Task StoreFaultWatch = Task.CompletedTask;
        }

        private FollowerOptions FollowerOptionsFor(EffectiveStartBlockResolver.EffectiveBounds bounds)
        {
            var options = EffectiveStartBlockResolver.BuildOptions(bounds, _config, _writeGate);
            return _headerFollow != null || _config.EngineOnly
                ? options with { ExternalHeaderFollow = true }
                : options;
        }

        private FollowerChainNode BuildFollowerNode()
        {
            var bounds = EffectiveStartBlockResolver.Resolve(
                _bundle.Metadata.GetSnapSyncState(),
                _bundle.Metadata.GetLastBlock(),
                _config);
            var options = FollowerOptionsFor(bounds);
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

                var options = FollowerOptionsFor(bounds);

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

        private Task LaunchStoreFaultWatch(CancellationTokenSource jobsCts, CancellationToken token)
        {
            if (_bundle is not IStoreFaultSource faultSource) return Task.CompletedTask;
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var fault = faultSource.FatalStoreFault;
                    if (fault != null)
                    {
                        _storeFatal = new InvalidOperationException($"store.fatal {fault}");
                        ReportFatal(_storeFatal);
                        jobsCts.Cancel();
                        return;
                    }
                    try { await Task.Delay(StoreFaultPollInterval, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }
            }, token);
        }

        private Task LaunchTipBandBodyFollow(CancellationToken stoppingToken)
        {
            Task tipBandFollowTask = Task.CompletedTask;
            if (_headerFollow != null && _scheduler != null && _pool != null)
            {
                var tipBandFollow = _tipBand ?? new TipBandBodyFollowService(
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
            if (_config.ReceiptBackfill && _scheduler != null && _pool != null)
            {
                var backfill = new ReceiptBackfillService(
                    _bundle,
                    _scheduler,
                    _pool,
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

        private async Task DrainBackgroundJobsAsync(BackgroundJobs jobs, CancellationToken jobsToken)
        {
            try { await jobs.SnapHistory.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "History backfill exited with error"); }
            try { await jobs.HistoryFollower.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "History backfill (after-state-sync) exited with error"); }
            try { await jobs.StateCompaction.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Background state compaction exited with error"); }
            try { await jobs.ReceiptScrub.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Receipt-backfill scrub exited with error"); }
            try { await jobs.TipBand.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Tip-band body follow exited with error"); }
            try { await jobs.HeaderFollow.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Headers job exited with error"); }
            try { await jobs.StoreFaultWatch.ConfigureAwait(false); }
            catch (OperationCanceledException) when (jobsToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Store fault watch exited with error"); }
        }
    }
}
