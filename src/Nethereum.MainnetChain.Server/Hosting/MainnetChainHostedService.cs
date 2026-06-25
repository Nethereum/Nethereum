using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.MainnetChain.Server.Bootstrap;
using Nethereum.MainnetChain.Server.Configuration;

namespace Nethereum.MainnetChain.Server.Hosting
{
    /// <summary>
    /// Drives the <see cref="MainnetChainNode.RunAsync"/> follower loop for the lifetime of
    /// the host. The chain node, block source, executor factory, validation policy and
    /// canonical-state-root source are composed by the DI container; this service simply
    /// owns the cancellation linkage to <c>IHostApplicationLifetime</c> and reports the
    /// terminal <see cref="FollowerRunResult"/> on shutdown.
    ///
    /// <para>
    /// Follower options are computed at runtime via
    /// <see cref="EffectiveStartBlockResolver"/> AFTER snap-bootstrap has had a chance
    /// to run; if a snap session just completed, the resolver advances StartBlock to
    /// <c>pivot + 1</c> so the executor doesn't try to replay blocks the snap path
    /// never fetched. EndBlock is recomputed relative to the effective start so a
    /// finite replay window of <c>Blocks</c> blocks remains a window of the same
    /// length regardless of which branch was taken.
    /// </para>
    /// </summary>
    public sealed class MainnetChainHostedService : BackgroundService
    {
        private readonly MainnetChainNodeFactory _nodeFactory;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockSource _source;
        private readonly IValidationPolicy _policy;
        private readonly ICanonicalStateRootSource? _canonical;
        private readonly MainnetChainServerConfig _config;
        private readonly ILogger<MainnetChainHostedService> _logger;
        private readonly IPeerPool? _pool;
        private readonly IFetchRequestScheduler? _scheduler;
        private readonly MainnetChainNodeAccessor _nodeAccessor;
        private readonly SnapSyncMetrics? _metrics;
        private FollowerRunResult? _lastResult;

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
            SnapSyncMetrics? metrics = null)
        {
            _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _nodeAccessor = nodeAccessor ?? throw new ArgumentNullException(nameof(nodeAccessor));
            _canonical = canonical;
            _pool = pool;
            _scheduler = scheduler;
            _metrics = metrics;
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
                // One-shot snap-bootstrap-state wipe — opt-in via env var.
                // Wipes only the state / trie / state-history column families
                // and the SnapSyncState metadata key. Headers, bodies,
                // transactions, receipts, logs, log indexes, blooms and the
                // metadata cursors are all preserved — the Phase 1 archive
                // stays intact and resumes from its existing cursor. Intended
                // for recovering from a corrupt or partial snap session
                // without re-fetching validated chain data. Set
                // NETHEREUM_WIPE_SNAP_STATE=1 once, restart, then unset.
                if (string.Equals(Environment.GetEnvironmentVariable("NETHEREUM_WIPE_SNAP_STATE"), "1", StringComparison.Ordinal))
                {
                    _logger.LogWarning("NETHEREUM_WIPE_SNAP_STATE=1 — wiping state / trie / state-history CFs and SnapSyncState metadata; receipts, logs and Phase 1 cursors are preserved.");
                    await _bundle.ResetSnapBootstrapStateAsync(stoppingToken).ConfigureAwait(false);
                    _logger.LogWarning("Snap-bootstrap state wipe complete; Phase 1 archive untouched. Snap bootstrap will re-stream from a fresh pivot.");
                }

                // Optional cold-start snap bootstrap. No-op when SnapBootstrap=false
                // or the bundle already has committed state. Runs synchronously before
                // the follower so post-pivot block execution sees the trie + bytecode
                // already populated and the metadata cursor at the pivot block.
                var snapResult = await SnapBootstrapInvoker.RunIfConfiguredAsync(
                    _bundle, _pool, _scheduler, _config, _logger, stoppingToken, _canonical, _metrics).ConfigureAwait(false);
                if (!snapResult.Ran && !string.IsNullOrEmpty(snapResult.SkipReason))
                {
                    _logger.LogInformation("Snap-bootstrap: not run — {Reason}.", snapResult.SkipReason);
                }

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
                var node = _nodeFactory.Build(_bundle, _source, _policy, options, _canonical);
                _nodeAccessor.Set(node);

                // Optional concurrent receipt-backfill scrub. Re-fetches receipts
                // over the already-synced range, validates each batch's Patricia
                // root against the stored header's ReceiptHash, and overwrites
                // entries with freshly-computed metadata. Runs alongside the
                // follower; cursor persisted to metadata for resume across
                // restarts. Off by default — opt-in via config.
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

                _lastResult = await node.RunAsync(stoppingToken, _logger).ConfigureAwait(false);
                _logger.LogInformation(
                    "MainnetChain follower exited: reason={Reason}, last_block={Last}, executed={Executed}, root_mismatches={Mismatches}",
                    _lastResult.ExitReason,
                    _lastResult.LastExecutedBlock,
                    _lastResult.BlocksExecuted,
                    _lastResult.RootMismatches);

                // Drain the scrub task on shutdown so its logs don't get cut off mid-batch.
                try { await receiptBackfillTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { _logger.LogWarning(ex, "Receipt-backfill scrub exited with error"); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MainnetChain follower cancelled.");
            }
        }
    }
}
