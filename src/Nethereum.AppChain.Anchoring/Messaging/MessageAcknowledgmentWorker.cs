using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageAcknowledgmentWorker : IHostedService, IDisposable
    {
        private readonly IMessageMerkleAccumulator _accumulator;
        private readonly Dictionary<ulong, IMessageAcknowledgmentService> _ackServices;
        private readonly MessageAcknowledgmentConfig _config;
        private readonly ILogger<MessageAcknowledgmentWorker>? _logger;

        private readonly ConcurrentDictionary<ulong, ulong> _lastAcknowledgedIds = new();
        private Timer? _timer;
        private int _isProcessing;
        private volatile bool _isRunning;

        public bool IsRunning => _isRunning;

        public MessageAcknowledgmentWorker(
            IMessageMerkleAccumulator accumulator,
            Dictionary<ulong, IMessageAcknowledgmentService> ackServices,
            MessageAcknowledgmentConfig config,
            ILogger<MessageAcknowledgmentWorker>? logger = null)
        {
            _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
            _ackServices = ackServices ?? throw new ArgumentNullException(nameof(ackServices));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_config.Enabled)
            {
                _logger?.LogInformation("Message acknowledgment worker is disabled");
                return Task.CompletedTask;
            }

            _logger?.LogInformation(
                "Message acknowledgment worker starting (interval={IntervalMs}ms, chains={ChainCount})",
                _config.IntervalMs, _ackServices.Count);

            _isRunning = true;
            _timer = new Timer(
                async _ =>
                {
                    try { await ProcessAcknowledgmentsAsync(); }
                    catch (Exception ex) { _logger?.LogError(ex, "Unhandled error in acknowledgment timer callback"); }
                },
                null,
                TimeSpan.FromMilliseconds(_config.IntervalMs),
                TimeSpan.FromMilliseconds(_config.IntervalMs));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Message acknowledgment worker stopping");
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async Task ProcessAcknowledgmentsAsync()
        {
            if (!_isRunning) return;
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

            try
            {
                var sourceChainIds = _accumulator.GetSourceChainIds();
                var tasks = new List<Task>();

                foreach (var sourceChainId in sourceChainIds)
                {
                    if (!_ackServices.TryGetValue(sourceChainId, out var ackService))
                        continue;

                    var (root, lastProcessed) = _accumulator.GetSnapshot(sourceChainId);
                    _lastAcknowledgedIds.TryGetValue(sourceChainId, out var lastAcknowledged);

                    if (lastProcessed <= lastAcknowledged)
                        continue;

                    if (root == null || root.Length == 0)
                        continue;

                    tasks.Add(AcknowledgeWithRetryAsync(
                        ackService, sourceChainId, lastProcessed, root));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during acknowledgment processing");
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        private async Task AcknowledgeWithRetryAsync(
            IMessageAcknowledgmentService ackService,
            ulong sourceChainId,
            ulong processedUpToMessageId,
            byte[] merkleRoot)
        {
            for (int attempt = 0; attempt < _config.MaxRetries; attempt++)
            {
                var success = await ackService.AcknowledgeMessagesAsync(
                    sourceChainId, processedUpToMessageId, merkleRoot);

                if (success)
                {
                    _lastAcknowledgedIds[sourceChainId] = processedUpToMessageId;
                    return;
                }

                if (attempt < _config.MaxRetries - 1)
                {
                    _logger?.LogWarning(
                        "Acknowledgment attempt {Attempt} failed for source chain {SourceChainId}, retrying in {DelayMs}ms",
                        attempt + 1, sourceChainId, _config.RetryDelayMs);
                    await Task.Delay(_config.RetryDelayMs * (int)Math.Pow(2, attempt));
                }
            }

            _logger?.LogError(
                "All {MaxRetries} acknowledgment attempts failed for source chain {SourceChainId}",
                _config.MaxRetries, sourceChainId);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
