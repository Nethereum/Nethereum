using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection(HostedServiceProcessExitCodeCollection.Name)]
    public class MainnetChainHostedServiceFatalTests
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

        [Fact]
        public async Task Given_AFollowerStackThatThrows_When_TheServiceRuns_Then_ItIsLoggedCriticalWithTheExceptionAndTheExitCodeIsOne()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                using var inner = InMemoryChainStoreBundle.Open();
                var bundle = HostedServiceTestSupport.BundleWhoseMetadataThrows(
                    inner, _ => { }, null, "GetSnapSyncState", "GetLastBlock");
                await using var provider = HostedServiceTestSupport.BuildProvider(bundle);
                var logger = new RecordingLogger();
                using var service = HostedServiceTestSupport.CreateService(provider, logger);

                try { await service.StartAsync(CancellationToken.None).WaitAsync(Bound); }
                catch (FatalProbeException) { }
                try { await service.ExecuteTask!.WaitAsync(Bound); }
                catch (FatalProbeException) { }
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);

                var critical = Assert.Single(logger.Entries, e => e.Level == LogLevel.Critical);
                Assert.IsType<FatalProbeException>(critical.Exception);
                Assert.Contains("follower.fatal", critical.Message);
                Assert.Equal(1, Environment.ExitCode);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        [Fact]
        public async Task Given_AServiceWhoseStopTokenIsCancelledMidStartup_When_TheServiceRuns_Then_ItLogsCancelledAtInformationWithNoCriticalAndTheExitCodeIsUnchanged()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                using var cts = new CancellationTokenSource();
                using var inner = InMemoryChainStoreBundle.Open();
                var bundle = HostedServiceTestSupport.BundleWhoseMetadataThrows(
                    inner,
                    method =>
                    {
                        if (method.Name != "GetSnapSyncState") return;
                        cts.Cancel();
                        throw new OperationCanceledException(cts.Token);
                    });
                await using var provider = HostedServiceTestSupport.BuildProvider(bundle);
                var logger = new RecordingLogger();
                using var service = HostedServiceTestSupport.CreateService(provider, logger);

                await service.StartAsync(cts.Token).WaitAsync(Bound);
                await service.ExecuteTask!.WaitAsync(Bound);
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);

                Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Critical);
                Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("cancelled"));
                Assert.Equal(0, Environment.ExitCode);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }
    }
}
