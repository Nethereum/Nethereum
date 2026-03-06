using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sync;

namespace Nethereum.AppChain.Server.Hosting
{
    public class SequencerHealthCheck : IHealthCheck
    {
        private readonly ISequencer? _sequencer;

        public SequencerHealthCheck(ISequencer? sequencer = null)
        {
            _sequencer = sequencer;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_sequencer == null)
            {
                return HealthCheckResult.Healthy("Follower mode");
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
        private readonly ILiveBlockSync? _liveSync;

        public SyncHealthCheck(ILiveBlockSync? liveSync = null)
        {
            _liveSync = liveSync;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_liveSync == null)
            {
                return Task.FromResult(HealthCheckResult.Healthy("No sync configured"));
            }

            if (_liveSync.State == LiveSyncState.Error)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Sync error state"));
            }

            var lag = _liveSync.RemoteTip - _liveSync.LocalTip;
            if (lag > 100)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Sync lag: {lag} blocks (local={_liveSync.LocalTip}, remote={_liveSync.RemoteTip})"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Synced (local={_liveSync.LocalTip}, remote={_liveSync.RemoteTip})"));
        }
    }
}
