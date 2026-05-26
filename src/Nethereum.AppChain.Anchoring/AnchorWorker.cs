using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring
{
    public class AnchorWorker : IHostedService, IDisposable
    {
        private readonly IChainAnchorable _chain;
        private readonly IAnchorService _anchorService;
        private readonly AnchorConfig _config;
        private readonly AnchorPublicationPipeline _pipeline;
        private readonly IAnchorSubmissionStrategy _strategy;
        private readonly AnchoringMetrics _metrics;
        private readonly ILogger<AnchorWorker> _logger;

        private Timer _timer;
        private readonly object _stateLock = new();
        private BigInteger _lastAnchoredBlock;
        private volatile bool _isRunning;
        private int _isAnchoring;

        public AnchorWorker(
            IChainAnchorable chain,
            IAnchorService anchorService,
            AnchorConfig config,
            AnchorPublicationPipeline pipeline = null,
            IAnchorSubmissionStrategy strategy = null,
            AnchoringMetrics metrics = null,
            ILogger<AnchorWorker> logger = null)
        {
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pipeline = pipeline;
            _strategy = strategy ?? Strategies.AnchoringStrategyFactory.Create(
                config.DataAvailability, config.ProofMode);
            _metrics = metrics;
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

            await _anchorService.InitializeAsync().ConfigureAwait(false);
            _lastAnchoredBlock = await _anchorService.GetLatestAnchoredBlockAsync().ConfigureAwait(false);
            _logger?.LogInformation("Anchor worker starting: strategy={Strategy}, cadence={Cadence}, lastBlock={Block}",
                _strategy.Name, _config.AnchorCadence, _lastAnchoredBlock);

            _isRunning = true;
            _timer = new Timer(
                async _ =>
                {
                    try { await AnchorIfNeededAsync().ConfigureAwait(false); }
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

                var currentBlockNumber = await _chain.GetBlockNumberAsync().ConfigureAwait(false);
                _metrics?.UpdateBatchAge((long)(currentBlockNumber - lastAnchored));

                var nextAnchorBlock = lastAnchored == 0
                    ? _config.AnchorCadence
                    : lastAnchored + _config.AnchorCadence;

                if (currentBlockNumber >= nextAnchorBlock)
                {
                    var blockToAnchor = (currentBlockNumber / _config.AnchorCadence) * _config.AnchorCadence;
                    if (blockToAnchor > lastAnchored)
                    {
                        await AnchorBlockAsync(blockToAnchor).ConfigureAwait(false);
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
            var block = await _chain.GetBlockByNumberAsync(blockNumber).ConfigureAwait(false);
            if (block == null)
            {
                _logger?.LogWarning("Block {BlockNumber} not found for anchoring", blockNumber);
                return;
            }

            var blockHash = await _chain.GetBlockHashByNumberAsync(blockNumber).ConfigureAwait(false);

            _logger?.LogInformation("Anchoring block {BlockNumber}, strategy={Strategy}", blockNumber, _strategy.Name);

            var scope = new AnchorScope
            {
                ChainId = (long)_config.ChainId,
                Kind = _config.AnchorCadence == 1 ? AnchorKind.Block : AnchorKind.Batch,
                StartBlock = (long)(_lastAnchoredBlock + 1),
                EndBlock = (long)blockNumber,
                StateRoot = block.StateRoot,
                TransactionsRoot = block.TransactionsHash,
                ReceiptsRoot = block.ReceiptHash,
                BlockHash = blockHash
            };

            AnchorPublicationResult? pubResult = null;
            if (_pipeline != null)
            {
                pubResult = await _pipeline.ExecuteAsync(scope).ConfigureAwait(false);

                if (pubResult.PreviousValidatedBlock.HasValue)
                    _logger?.LogInformation("Proof pointer advanced to block {Block}",
                        pubResult.PreviousValidatedBlock.Value);
            }

            var submissionContext = new AnchorSubmissionContext
            {
                Scope = scope,
                PipelineResult = pubResult,
                BlockRlp = Nethereum.Model.BlockHeaderEncoder.Current.Encode(block)
            };

            var submission = _strategy.BuildPayload(submissionContext);
            _logger?.LogInformation("Anchor block {Block}: {Description}, payload={Size}b",
                blockNumber, submission.Description, submission.ProofBytes.Length);

            var sw = Stopwatch.StartNew();
            var retries = 0;
            while (retries < _config.MaxRetries)
            {
                var result = await _anchorService.AnchorBlockAsync(
                    blockNumber, block.StateRoot, block.TransactionsHash, block.ReceiptHash,
                    blockHash, submission).ConfigureAwait(false);

                if (result.Status == AnchorStatus.Confirmed)
                {
                    sw.Stop();
                    lock (_stateLock) { _lastAnchoredBlock = blockNumber; }

                    _metrics?.RecordAnchor((long)blockNumber, result.GasUsed, sw.Elapsed.TotalSeconds);

                    if (_pipeline != null && result.AnchorTxHash != null && pubResult?.EncodedPayload != null)
                        _pipeline.RecordAnchorTx((long)blockNumber, result.AnchorTxHash, pubResult.EncodedPayload);

                    _logger?.LogInformation("Successfully anchored block {BlockNumber} in {Elapsed:F1}s",
                        blockNumber, sw.Elapsed.TotalSeconds);
                    return;
                }

                retries++;
                _metrics?.RecordError("submission_failed");
                if (retries < _config.MaxRetries)
                {
                    _logger?.LogWarning("Anchor attempt {Attempt} failed for block {BlockNumber}, retrying...",
                        retries, blockNumber);
                    await Task.Delay(_config.RetryDelayMs * (int)Math.Pow(2, retries - 1)).ConfigureAwait(false);
                }
            }

            _metrics?.RecordError("max_retries_exhausted");
            _logger?.LogError("Failed to anchor block {BlockNumber} after {MaxRetries} attempts",
                blockNumber, _config.MaxRetries);
        }

        public async Task ForceAnchorAsync(BigInteger? blockNumber = null)
        {
            var targetBlock = blockNumber ?? await _chain.GetBlockNumberAsync().ConfigureAwait(false);
            await AnchorBlockAsync(targetBlock).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
