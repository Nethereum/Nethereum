using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Sync.Hosting
{
    public class SyncHostedService : IHostedService
    {
        private readonly CoordinatedSyncService _syncService;
        private readonly bool _alreadyStarted;
        private readonly ILogger<SyncHostedService>? _logger;

        public SyncHostedService(
            CoordinatedSyncService syncService,
            bool alreadyStarted = false,
            ILogger<SyncHostedService>? logger = null)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _alreadyStarted = alreadyStarted;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_alreadyStarted)
            {
                _logger?.LogInformation("Sync service already started, skipping StartAsync");
                return;
            }

            _logger?.LogInformation("Starting sync service...");
            await _syncService.StartAsync(cancellationToken);
            _logger?.LogInformation("Sync service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Stopping sync service...");
            await _syncService.StopAsync();
            _logger?.LogInformation("Sync service stopped");
        }
    }
}
