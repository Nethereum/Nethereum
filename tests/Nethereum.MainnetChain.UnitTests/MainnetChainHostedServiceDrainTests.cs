using System.Diagnostics;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection(HostedServiceProcessExitCodeCollection.Name)]
    public class MainnetChainHostedServiceDrainTests
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

        private sealed class StragglerTipSource : ICanonicalStateRootSource
        {
            private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<long> _returned = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public string Name => "straggler-tip";

            public Task Entered => _entered.Task;

            public Task<long> Returned => _returned.Task;

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((Array.Empty<byte>(), Array.Empty<byte>()));

            public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                _entered.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { }
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                _returned.TrySetResult(Stopwatch.GetTimestamp());
                throw new OperationCanceledException(ct);
            }
        }

        [Fact]
        public async Task Given_AFaultingFollowerAndARunningHeadersJob_When_TheHostStops_Then_TheBundleIsDisposedOnlyAfterTheHeadersJobEnded()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                var tipSource = new StragglerTipSource();
                var headerFollow = new HeaderFollowService(
                    tipSource,
                    (_, _, _, _, _, _) => Task.FromException<WalkerOutcome>(new InvalidOperationException("walker is never reached")));

                long disposedAt = 0;
                using var inner = InMemoryChainStoreBundle.Open();
                var bundle = HostedServiceTestSupport.BundleWhoseMetadataThrows(
                    inner,
                    method =>
                    {
                        if (method.Name == "GetSnapSyncState")
                            tipSource.Entered.Wait(Bound);
                    },
                    () => Interlocked.CompareExchange(ref disposedAt, Stopwatch.GetTimestamp(), 0),
                    "GetSnapSyncState");
                var provider = HostedServiceTestSupport.BuildProvider(bundle);
                var logger = new RecordingLogger();
                using var service = HostedServiceTestSupport.CreateService(provider, logger, headerFollow);

                try { await service.StartAsync(CancellationToken.None).WaitAsync(Bound); }
                catch (FatalProbeException) { }
                try { await service.ExecuteTask!.WaitAsync(Bound); }
                catch (FatalProbeException) { }
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);
                await provider.DisposeAsync();

                var headersJobEndedAt = await tipSource.Returned.WaitAsync(Bound);
                Assert.NotEqual(0, Interlocked.Read(ref disposedAt));
                Assert.True(
                    headersJobEndedAt <= Interlocked.Read(ref disposedAt),
                    "the bundle was disposed while the headers job was still running");
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }
    }
}
