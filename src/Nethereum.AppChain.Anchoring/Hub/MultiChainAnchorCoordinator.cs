using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public class MultiChainAnchorCoordinator : IHostedService, IDisposable
    {
        private readonly IChainAnchorable _chain;
        private readonly Dictionary<ulong, HubAnchorService> _anchorServices;
        private readonly MultiChainHubConfig _config;
        private readonly ILogger<MultiChainAnchorCoordinator>? _logger;

        private readonly ConcurrentDictionary<ulong, BigInteger> _lastAnchoredPerChain = new();
        private Timer? _timer;
        private int _isAnchoring;
        private volatile bool _isRunning;

        public bool IsRunning => _isRunning;

        public MultiChainAnchorCoordinator(
            IChainAnchorable chain,
            Dictionary<ulong, HubAnchorService> anchorServices,
            MultiChainHubConfig config,
            ILogger<MultiChainAnchorCoordinator>? logger = null)
        {
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _anchorServices = anchorServices ?? throw new ArgumentNullException(nameof(anchorServices));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_config.Enabled)
            {
                _logger?.LogInformation("Multi-chain anchor coordinator is disabled");
                return Task.CompletedTask;
            }

            _logger?.LogInformation(
                "Multi-chain anchor coordinator starting (interval={IntervalMs}ms, cadence={Cadence}, chains={ChainCount})",
                _config.AnchorIntervalMs, _config.AnchorCadence, _anchorServices.Count);

            _isRunning = true;
            _timer = new Timer(
                async _ =>
                {
                    try { await AnchorAllChainsAsync(); }
                    catch (Exception ex) { _logger?.LogError(ex, "Unhandled error in multi-chain anchor timer callback"); }
                },
                null,
                TimeSpan.FromMilliseconds(_config.AnchorIntervalMs),
                TimeSpan.FromMilliseconds(_config.AnchorIntervalMs));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Multi-chain anchor coordinator stopping");
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async Task AnchorAllChainsAsync()
        {
            if (!_isRunning) return;
            if (Interlocked.CompareExchange(ref _isAnchoring, 1, 0) != 0) return;

            try
            {
                var currentBlock = await _chain.GetBlockNumberAsync();
                var header = await _chain.GetBlockByNumberAsync(currentBlock);
                if (header == null) return;

                var tasks = new List<Task>();

                foreach (var (chainId, anchorService) in _anchorServices)
                {
                    _lastAnchoredPerChain.TryGetValue(chainId, out var lastAnchored);
                    var blocksSinceAnchor = currentBlock - lastAnchored;

                    if (blocksSinceAnchor < _config.AnchorCadence)
                        continue;

                    tasks.Add(AnchorToChainAsync(chainId, anchorService, currentBlock, header));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during multi-chain anchoring");
            }
            finally
            {
                Interlocked.Exchange(ref _isAnchoring, 0);
            }
        }

        private async Task AnchorToChainAsync(
            ulong chainId,
            HubAnchorService anchorService,
            BigInteger blockNumber,
            Nethereum.Model.BlockHeader header)
        {
            try
            {
                var result = await anchorService.AnchorBlockAsync(
                    blockNumber,
                    header.StateRoot,
                    header.TransactionsHash,
                    header.ReceiptHash);

                if (result.Status == AnchorStatus.Confirmed)
                {
                    _lastAnchoredPerChain[chainId] = blockNumber;
                    _logger?.LogInformation(
                        "Block {BlockNumber} anchored to chain {ChainId}",
                        blockNumber, chainId);
                }
                else
                {
                    _logger?.LogWarning(
                        "Failed to anchor block {BlockNumber} to chain {ChainId}: {Error}",
                        blockNumber, chainId, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error anchoring block {BlockNumber} to chain {ChainId}",
                    blockNumber, chainId);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
