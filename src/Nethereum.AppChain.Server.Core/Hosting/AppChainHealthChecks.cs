using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.AppChain.Server.Hosting
{
    public class SequencerHealthCheck : IHealthCheck
    {
        private readonly ISequencer? _sequencer;
        private readonly IProducerAuthority? _producerAuthority;
        private readonly string? _ourNodeId;

        public SequencerHealthCheck(
            ISequencer? sequencer = null,
            IProducerAuthority? producerAuthority = null,
            string? ourNodeId = null)
        {
            _sequencer = sequencer;
            _producerAuthority = producerAuthority;
            _ourNodeId = ourNodeId;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_sequencer == null)
            {
                return HealthCheckResult.Healthy("Follower mode");
            }

            if (_producerAuthority != null)
            {
                var currentProducer = _producerAuthority.CurrentProducer();
                if (!string.Equals(currentProducer, _ourNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return HealthCheckResult.Unhealthy(
                        $"Not the current sequencer (authority names {currentProducer ?? "<none>"})");
                }
            }

            try
            {
                var blockNumber = await _sequencer.GetBlockNumberAsync();
                return HealthCheckResult.Healthy($"Block #{blockNumber}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Sequencer error", ex);
            }
        }
    }

    public class SyncHealthCheck : IHealthCheck
    {
        private readonly IPeerPool? _peers;

        public SyncHealthCheck(IPeerPool? peers = null)
        {
            _peers = peers;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_peers == null)
                return Task.FromResult(HealthCheckResult.Healthy("No peer pool configured"));

            var connected = _peers.ActivePeers.Count;
            return Task.FromResult(connected == 0
                ? HealthCheckResult.Degraded("No peers connected")
                : HealthCheckResult.Healthy($"{connected} peers connected"));
        }
    }
}
