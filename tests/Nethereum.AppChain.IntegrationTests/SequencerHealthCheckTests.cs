using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.CoreChain;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class SequencerHealthCheckTests
    {
        private sealed class StubSequencer : ISequencer
        {
            public SequencerConfig Config => throw new NotImplementedException();
            public IAppChain AppChain => throw new NotImplementedException();
            public CoreChain.ITxPool TxPool => throw new NotImplementedException();
            public IPolicyEnforcer PolicyEnforcer => throw new NotImplementedException();

            public event EventHandler<BlockProductionResult>? BlockProduced;

            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;
            public Task<byte[]> SubmitTransactionAsync(ISignedTransaction transaction) => throw new NotImplementedException();
            public Task<byte[]> ProduceBlockAsync() => throw new NotImplementedException();
            public Task<BigInteger> GetBlockNumberAsync() => Task.FromResult(new BigInteger(7));
            public Task<BlockHeader?> GetLatestBlockAsync() => throw new NotImplementedException();
        }

        private sealed class StaticAuthority : IProducerAuthority
        {
            private readonly string? _holder;
            public StaticAuthority(string? holder) => _holder = holder;
            public string? CurrentProducer() => _holder;
        }

        [Fact]
        public async Task Given_NoSequencerAtAll_When_HealthIsChecked_Then_ItIsHealthyFollowerMode()
        {
            var check = new SequencerHealthCheck(sequencer: null);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task Given_NoProducerAuthorityIsConfigured_When_HealthIsChecked_Then_ItFallsBackToSequencerHealth()
        {
            var check = new SequencerHealthCheck(new StubSequencer(), producerAuthority: null, ourNodeId: null);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task Given_TheProducerAuthorityNamesThisNode_When_HealthIsChecked_Then_ItIsHealthy()
        {
            var check = new SequencerHealthCheck(new StubSequencer(), new StaticAuthority("A"), "A");

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task Given_TheProducerAuthorityNamesADifferentNode_When_HealthIsChecked_Then_ItIsUnhealthy()
        {
            var check = new SequencerHealthCheck(new StubSequencer(), new StaticAuthority("B"), "A");

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }

        [Fact]
        public async Task Given_TheProducerAuthorityNamesNobody_When_HealthIsChecked_Then_ItIsUnhealthy()
        {
            var check = new SequencerHealthCheck(new StubSequencer(), new StaticAuthority(null), "A");

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
    }
}
