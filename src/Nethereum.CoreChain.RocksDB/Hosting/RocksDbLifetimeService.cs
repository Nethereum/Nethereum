using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.CoreChain.RocksDB.Hosting
{
    public class RocksDbLifetimeService : IHostedService, IDisposable
    {
        private readonly RocksDbManager _manager;
        private readonly ILogger<RocksDbLifetimeService> _logger;

        public RocksDbLifetimeService(
            RocksDbManager manager,
            ILogger<RocksDbLifetimeService> logger = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Disposing RocksDB...");
            _manager.Dispose();
            _logger?.LogInformation("RocksDB disposed");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _manager.Dispose();
        }
    }
}
