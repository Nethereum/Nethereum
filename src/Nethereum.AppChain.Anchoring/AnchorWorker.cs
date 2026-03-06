using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Anchoring
{
    public class AnchorWorker : IHostedService, IDisposable
    {
        private readonly IChainAnchorable _chain;
        private readonly IAnchorService _anchorService;
        private readonly AnchorConfig _config;
        private readonly ILogger<AnchorWorker>? _logger;

        private Timer? _timer;
        private readonly object _stateLock = new();
        private BigInteger _lastAnchoredBlock;
        private volatile bool _isRunning;
        private int _isAnchoring;

        public AnchorWorker(
            IChainAnchorable chain,
            IAnchorService anchorService,
            AnchorConfig config,
            ILogger<AnchorWorker>? logger = null)
        {
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        public BigInteger LastAnchoredBlock
        {
            get { lock (_stateLock) { return _lastAnchoredBlock; } }
        }

        public bool IsRunning => _isRunning;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_config.Enabled)
            {
                _logger?.LogInformation("Anchor worker is disabled");
                return;
            }

            _lastAnchoredBlock = await _anchorService.GetLatestAnchoredBlockAsync();
            _logger?.LogInformation("Anchor worker starting, last anchored block: {BlockNumber}", _lastAnchoredBlock);

            _isRunning = true;
            _timer = new Timer(
                async _ =>
                {
                    try { await AnchorIfNeededAsync(); }
                    catch (Exception ex) { _logger?.LogError(ex, "Unhandled error in anchor timer callback"); }
                },
                null,
                TimeSpan.FromMilliseconds(_config.AnchorIntervalMs),
                TimeSpan.FromMilliseconds(_config.AnchorIntervalMs));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Anchor worker stopping");
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async Task AnchorIfNeededAsync()
        {
            if (!_isRunning)
                return;
            if (Interlocked.CompareExchange(ref _isAnchoring, 1, 0) != 0)
                return;

            try
            {
                BigInteger lastAnchored;
                lock (_stateLock) { lastAnchored = _lastAnchoredBlock; }

                var currentBlockNumber = await _chain.GetBlockNumberAsync();

                var nextAnchorBlock = lastAnchored == 0
                    ? _config.AnchorCadence
                    : lastAnchored + _config.AnchorCadence;

                if (currentBlockNumber >= nextAnchorBlock)
                {
                    var blockToAnchor = (currentBlockNumber / _config.AnchorCadence) * _config.AnchorCadence;
                    if (blockToAnchor > lastAnchored)
                    {
                        await AnchorBlockAsync(blockToAnchor);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking for anchor");
            }
            finally
            {
                Interlocked.Exchange(ref _isAnchoring, 0);
            }
        }

        private async Task AnchorBlockAsync(BigInteger blockNumber)
        {
            var block = await _chain.GetBlockByNumberAsync(blockNumber);
            if (block == null)
            {
                _logger?.LogWarning("Block {BlockNumber} not found for anchoring", blockNumber);
                return;
            }

            _logger?.LogInformation("Anchoring block {BlockNumber}", blockNumber);

            var retries = 0;
            while (retries < _config.MaxRetries)
            {
                var result = await _anchorService.AnchorBlockAsync(
                    blockNumber,
                    block.StateRoot,
                    block.TransactionsHash,
                    block.ReceiptHash);

                if (result.Status == AnchorStatus.Confirmed)
                {
                    lock (_stateLock) { _lastAnchoredBlock = blockNumber; }
                    _logger?.LogInformation("Successfully anchored block {BlockNumber}", blockNumber);
                    return;
                }

                retries++;
                if (retries < _config.MaxRetries)
                {
                    _logger?.LogWarning("Anchor attempt {Attempt} failed for block {BlockNumber}, retrying...",
                        retries, blockNumber);
                    await Task.Delay(_config.RetryDelayMs * (int)Math.Pow(2, retries - 1));
                }
            }

            _logger?.LogError("Failed to anchor block {BlockNumber} after {MaxRetries} attempts",
                blockNumber, _config.MaxRetries);
        }

        public async Task ForceAnchorAsync(BigInteger? blockNumber = null)
        {
            var targetBlock = blockNumber ?? await _chain.GetBlockNumberAsync();
            await AnchorBlockAsync(targetBlock);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
