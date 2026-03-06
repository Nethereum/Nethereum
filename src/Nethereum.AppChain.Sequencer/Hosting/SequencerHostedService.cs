using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Sequencer.Hosting
{
    public class SequencerHostedService : IHostedService
    {
        private readonly ISequencer _sequencer;
        private readonly bool _alreadyStarted;
        private readonly ILogger<SequencerHostedService>? _logger;

        public SequencerHostedService(
            ISequencer sequencer,
            bool alreadyStarted = false,
            ILogger<SequencerHostedService>? logger = null)
        {
            _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            _alreadyStarted = alreadyStarted;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_alreadyStarted)
            {
                _logger?.LogInformation("Sequencer already started, skipping StartAsync");
                return;
            }

            _logger?.LogInformation("Starting sequencer...");
            await _sequencer.StartAsync(cancellationToken);
            _logger?.LogInformation("Sequencer started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Stopping sequencer...");
            await _sequencer.StopAsync();
            _logger?.LogInformation("Sequencer stopped");
        }
    }
}
