using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;

namespace Nethereum.DevChain.Server.Hosting
{
    public class DevChainHostedService : IHostedService, IDisposable
    {
        private readonly DevChainNode _node;
        private readonly DevAccountManager _accountManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DevChainHostedService>? _logger;
        private bool _stopped;

        public bool AlreadyStarted { get; set; }

        public DevChainHostedService(
            DevChainNode node,
            DevAccountManager accountManager,
            IServiceProvider serviceProvider,
            ILogger<DevChainHostedService>? logger = null)
        {
            _node = node;
            _accountManager = accountManager;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (AlreadyStarted)
            {
                _logger?.LogInformation("DevChain node already started, skipping");
                return;
            }

            _logger?.LogInformation("Starting DevChain node...");
            await _node.StartAsync(_accountManager.Accounts.Select(a => a.Address));
            _logger?.LogInformation("DevChain node started");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_stopped) return Task.CompletedTask;
            _stopped = true;

            _logger?.LogInformation("Stopping DevChain node...");

            if (_node is IDisposable disposable)
                disposable.Dispose();

            var sqliteManager = _serviceProvider.GetService<Nethereum.DevChain.Storage.Sqlite.SqliteStorageManager>();
            sqliteManager?.Dispose();

            _logger?.LogInformation("DevChain node stopped");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_stopped && _node is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
