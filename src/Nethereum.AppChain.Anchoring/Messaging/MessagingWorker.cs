using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessagingWorker : IHostedService, IDisposable
    {
        private readonly IMessagingService _messagingService;
        private readonly MessagingConfig _config;
        private readonly ILogger<MessagingWorker>? _logger;

        private Timer? _timer;
        private volatile bool _isRunning;
        private int _isPolling;

        public MessagingWorker(
            IMessagingService messagingService,
            MessagingConfig config,
            ILogger<MessagingWorker>? logger = null)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        public bool IsRunning => _isRunning;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_config.Enabled)
            {
                _logger?.LogInformation("Messaging worker is disabled");
                return Task.CompletedTask;
            }

            _logger?.LogInformation("Messaging worker starting (pollInterval={PollInterval}ms, sources={SourceCount})",
                _config.PollIntervalMs, _config.SourceChains.Count);

            _isRunning = true;
            _timer = new Timer(
                async _ =>
                {
                    try { await PollAsync(); }
                    catch (Exception ex) { _logger?.LogError(ex, "Unhandled error in messaging timer callback"); }
                },
                null,
                TimeSpan.FromMilliseconds(_config.PollIntervalMs),
                TimeSpan.FromMilliseconds(_config.PollIntervalMs));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Messaging worker stopping");
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async Task PollAsync()
        {
            if (!_isRunning) return;
            if (Interlocked.CompareExchange(ref _isPolling, 1, 0) != 0) return;

            try
            {
                await _messagingService.PollAllSourcesAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during message polling");
            }
            finally
            {
                Interlocked.Exchange(ref _isPolling, 0);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
