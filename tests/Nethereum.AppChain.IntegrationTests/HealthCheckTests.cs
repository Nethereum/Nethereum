using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.DevP2P.Sync.Abstractions;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class HealthCheckTests
    {
        private sealed class PoolOf : IPeerPool
        {
            private readonly IEthPeer[] _peers;

            private PoolOf(int count) => _peers = new IEthPeer[count];

            public static IPeerPool Peers(int count) => new PoolOf(count);

            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;

            public int TargetPeerCount => _peers.Length;

            public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }

            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }

            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;

            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;

            public void ReportSuccess(Guid peerId) { }

            public Task ClearAllBansAsync() => Task.CompletedTask;

            public ValueTask DisposeAsync() => default;
        }

        [Fact]
        public async Task SequencerHealthCheck_NullSequencer_ReturnsHealthy()
        {
            var check = new SequencerHealthCheck(null);
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("Follower", result.Description);
        }

        [Fact]
        public async Task SyncHealthCheck_NoPeerPool_ReturnsHealthy()
        {
            var check = new SyncHealthCheck(null);
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("No peer pool", result.Description);
        }

        [Fact]
        public async Task SyncHealthCheck_PoolWithNoPeers_ReturnsDegraded()
        {
            var check = new SyncHealthCheck(PoolOf.Peers(0));
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Degraded, result.Status);
        }

        [Fact]
        public async Task SyncHealthCheck_PoolWithPeers_ReturnsHealthy()
        {
            var check = new SyncHealthCheck(PoolOf.Peers(2));
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("2 peers", result.Description);
        }
    }
}
