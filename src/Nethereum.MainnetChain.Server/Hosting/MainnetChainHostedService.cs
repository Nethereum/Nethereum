using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
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
    /// </summary>
    public sealed class MainnetChainHostedService : BackgroundService
    {
        private readonly MainnetChainNode _node;
        private readonly MainnetChainServerConfig _config;
        private readonly ILogger<MainnetChainHostedService> _logger;
        private FollowerRunResult? _lastResult;

        public MainnetChainHostedService(
            MainnetChainNode node,
            MainnetChainServerConfig config,
            ILogger<MainnetChainHostedService> logger)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                // Optional cold-start snap bootstrap. No-op when SnapBootstrap=false
                // or the bundle already has committed state. Runs synchronously before
                // the follower so post-pivot block execution sees the trie + bytecode
                // already populated and the metadata cursor at the pivot block.
                var snapResult = await SnapBootstrapInvoker.RunIfConfiguredAsync(
                    _node.Bundle, _config, _logger, stoppingToken).ConfigureAwait(false);
                if (!snapResult.Ran && !string.IsNullOrEmpty(snapResult.SkipReason))
                {
                    _logger.LogInformation("Snap-bootstrap: not run — {Reason}.", snapResult.SkipReason);
                }

                _lastResult = await _node.RunAsync(stoppingToken, _logger).ConfigureAwait(false);
                _logger.LogInformation(
                    "MainnetChain follower exited: reason={Reason}, last_block={Last}, executed={Executed}, root_mismatches={Mismatches}",
                    _lastResult.ExitReason,
                    _lastResult.LastExecutedBlock,
                    _lastResult.BlocksExecuted,
                    _lastResult.RootMismatches);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MainnetChain follower cancelled.");
            }
        }
    }
}
