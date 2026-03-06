using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.P2P;

namespace Nethereum.AppChain.P2P.Hosting
{
    public class P2PTransportHostedService : IHostedService, IDisposable
    {
        private readonly IP2PTransport _transport;
        private readonly bool _alreadyStarted;
        private readonly ILogger<P2PTransportHostedService>? _logger;

        public P2PTransportHostedService(
            IP2PTransport transport,
            bool alreadyStarted = false,
            ILogger<P2PTransportHostedService>? logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _alreadyStarted = alreadyStarted;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_alreadyStarted)
            {
                _logger?.LogInformation("P2P transport already started, skipping StartAsync");
                return;
            }

            _logger?.LogInformation("Starting P2P transport...");
            await _transport.StartAsync(cancellationToken);
            _logger?.LogInformation("P2P transport started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Stopping P2P transport...");
            await _transport.StopAsync();
            _logger?.LogInformation("P2P transport stopped");
        }

        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
