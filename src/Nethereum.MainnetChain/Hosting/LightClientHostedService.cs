using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Consensus.LightClient;
using Nethereum.Signer.Bls;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class LightClientHostedService : BackgroundService
    {
        private readonly LightClientService _service;
        private readonly IBls _bls;
        private readonly ILogger<LightClientHostedService> _logger;
        private readonly TimeSpan _pollInterval;

        public LightClientHostedService(
            LightClientService service,
            IBls bls,
            ILogger<LightClientHostedService> logger)
            : this(service, bls, logger, TimeSpan.FromSeconds(12))
        {
        }

        public LightClientHostedService(
            LightClientService service,
            IBls bls,
            ILogger<LightClientHostedService> logger,
            TimeSpan pollInterval)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _bls = bls ?? throw new ArgumentNullException(nameof(bls));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pollInterval = pollInterval;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_bls is NativeBls nativeBls)
            {
                try
                {
                    await nativeBls.InitializeAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BLS native init failed; light-client update verification may not work.");
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _service.InitializeAsync(stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation("Light client initialised against beacon endpoint.");
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Light client bootstrap failed (beacon endpoint not ready?); retrying in {Interval}.", _pollInterval);
                    try { await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _service.UpdateAsync(stoppingToken).ConfigureAwait(false);
                    var finApplied = await _service.UpdateFinalityAsync(stoppingToken).ConfigureAwait(false);
                    if (!finApplied)
                        _logger.LogInformation("lc.update.finality not_applied reason={Reason}", _service.LastRejectReason);
                    var optApplied = await _service.UpdateOptimisticAsync(stoppingToken).ConfigureAwait(false);
                    if (!optApplied)
                        _logger.LogInformation("lc.update.optimistic not_applied reason={Reason}", _service.LastRejectReason);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Light client update poll failed; retrying after {Interval}.", _pollInterval);
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
