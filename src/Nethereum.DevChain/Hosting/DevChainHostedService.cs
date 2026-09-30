using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;

namespace Nethereum.DevChain.Hosting
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

            var config = _serviceProvider.GetRequiredService<DevChainServerConfig>();
            var bundle = _serviceProvider.GetRequiredService<IChainStoreBundle>();
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;

            await DevChainComposition.ComposeAsync(
                config, _node, bundle,
                node => node.StartAsync(_accountManager.Accounts.Select(a => a.Address)),
                loggerFactory, cancellationToken);

            _logger?.LogInformation("DevChain node started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_stopped) return;
            _stopped = true;

            _logger?.LogInformation("Stopping DevChain node...");

            if (_node is IDisposable disposable)
                disposable.Dispose();

            var sqliteManager = _serviceProvider.GetService<Nethereum.DevChain.Storage.Sqlite.SqliteStorageManager>();
            sqliteManager?.Dispose();

            var rocksStorage = _serviceProvider.GetService<Nethereum.ChainNode.Hosting.ChainNodeStorage>();
            if (rocksStorage != null)
                await rocksStorage.DisposeAsync();

            _logger?.LogInformation("DevChain node stopped");
        }

        public void Dispose()
        {
            if (!_stopped && _node is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
