using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection(HostedServiceProcessExitCodeCollection.Name)]
    public class MainnetChainHostedServiceArmingTests
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

        private sealed class IdleTipSource : ICanonicalStateRootSource
        {
            public string Name => "idle-tip";

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((Array.Empty<byte>(), Array.Empty<byte>()));

            public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new OperationCanceledException(ct);
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Bound;
            while (DateTime.UtcNow < deadline && !condition())
                await Task.Delay(10);
        }

        [Fact]
        public async Task Given_NoHeaderFollow_When_TheHostRuns_Then_TheValveIsNeverArmed()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                using var inner = InMemoryChainStoreBundle.Open();
                var logger = new RecordingLogger();
                var (bundle, probe) = YieldProbeBundle.Wrap(inner, () => false);
                await using var provider = HostedServiceTestSupport.BuildProvider(bundle);
                using var service = HostedServiceTestSupport.CreateService(provider, logger);

                await service.StartAsync(CancellationToken.None).WaitAsync(Bound);
                await service.ExecuteTask!.WaitAsync(Bound);
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);

                Assert.Contains(logger.Entries, e => e.Message.Contains("MainnetChain follower exited"));
                Assert.DoesNotContain(probe.Calls, c => c.Armed);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        [Fact]
        public async Task Given_HeaderFollow_When_TheHostRuns_Then_TheValveIsArmedAfterSnapReturns()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                using var inner = InMemoryChainStoreBundle.Open();
                var logger = new RecordingLogger();
                var (bundle, probe) = YieldProbeBundle.Wrap(
                    inner,
                    () => logger.Entries.Any(e => e.Message.Contains("Snap-bootstrap: not run")));
                await using var provider = HostedServiceTestSupport.BuildProvider(bundle);
                var headerFollow = new HeaderFollowService(
                    new IdleTipSource(),
                    (_, _, _, _, _, _) => Task.FromException<WalkerOutcome>(new InvalidOperationException("walker is never reached")));
                using var service = HostedServiceTestSupport.CreateService(provider, logger, headerFollow);

                await service.StartAsync(CancellationToken.None).WaitAsync(Bound);
                await WaitUntilAsync(() => probe.Calls.Any(c => c.Armed));
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);
                await service.ExecuteTask!.WaitAsync(Bound);

                var calls = probe.Calls;
                Assert.Equal(2, calls.Count);
                Assert.True(calls[0].Armed);
                Assert.True(calls[0].SnapBootstrapReturned, "the valve was armed before the snap bootstrap returned");
                Assert.False(calls[1].Armed);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }
    }
}
