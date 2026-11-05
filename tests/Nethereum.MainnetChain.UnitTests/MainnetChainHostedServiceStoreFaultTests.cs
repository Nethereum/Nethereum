using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection(HostedServiceProcessExitCodeCollection.Name)]
    public class MainnetChainHostedServiceStoreFaultTests
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

        public interface IFaultingBundle : IChainStoreBundle, IStoreFaultSource
        {
        }

        public class FaultingBundleProxy : DispatchProxy
        {
            public IChainStoreBundle Inner { get; set; } = null!;

            public Func<string?> Fault { get; set; } = () => null;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null) return null;
                if (targetMethod.Name == "get_FatalStoreFault") return Fault();
                try
                {
                    return targetMethod.Invoke(Inner, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
        }

        private sealed class BlockingBlockSource : IBlockSource
        {
            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct);
                yield break;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(default(BlockSourceHealth));

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal? LastChainBreak => null;
        }

        [Fact]
        public async Task Given_ABundleThatReportsAFatalStoreFaultAfterStart_When_TheServiceRuns_Then_ItLogsFollowerFatalSetsExitCodeOneAndExecuteAsyncEnds()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                using var inner = InMemoryChainStoreBundle.Open();
                string? fault = null;
                var bundle = DispatchProxy.Create<IFaultingBundle, FaultingBundleProxy>();
                var proxy = (FaultingBundleProxy)(object)bundle;
                proxy.Inner = inner;
                proxy.Fault = () => Volatile.Read(ref fault);
                await using var provider = BuildProvider(bundle);
                var logger = new RecordingLogger();
                using var service = HostedServiceTestSupport.CreateService(provider, logger);

                await service.StartAsync(CancellationToken.None).WaitAsync(Bound);
                await Task.Delay(500);
                Assert.False(service.ExecuteTask!.IsCompleted);
                Volatile.Write(ref fault, "Corruption: block checksum mismatch");

                try { await service.ExecuteTask!.WaitAsync(Bound); }
                catch (InvalidOperationException) { }
                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);

                var critical = Assert.Single(logger.Entries, e => e.Level == LogLevel.Critical);
                Assert.Contains("follower.fatal", critical.Message);
                Assert.Contains("store.fatal Corruption: block checksum mismatch", critical.Message);
                Assert.Equal(1, Environment.ExitCode);
                Assert.True(service.ExecuteTask!.IsCompleted);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        [Fact]
        public async Task Given_ABundleThatReportsNoStoreFault_When_TheServiceRuns_Then_TheHostKeepsRunningWithNoFatalAndTheExitCodeUnchanged()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                using var inner = InMemoryChainStoreBundle.Open();
                var bundle = DispatchProxy.Create<IFaultingBundle, FaultingBundleProxy>();
                var proxy = (FaultingBundleProxy)(object)bundle;
                proxy.Inner = inner;
                proxy.Fault = () => null;
                await using var provider = BuildProvider(bundle);
                var logger = new RecordingLogger();
                using var service = HostedServiceTestSupport.CreateService(provider, logger);

                await service.StartAsync(CancellationToken.None).WaitAsync(Bound);
                await Task.Delay(TimeSpan.FromSeconds(7));

                Assert.False(service.ExecuteTask!.IsCompleted);
                Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Critical);
                Assert.Equal(0, Environment.ExitCode);

                await service.StopAsync(CancellationToken.None).WaitAsync(Bound);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }

        private static ServiceProvider BuildProvider(IChainStoreBundle bundle)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.UseInMemoryBundleAndSource(new BlockingBlockSource());
            services.AddMainnetChainServer(new MainnetChainServerConfig { DataDir = null });
            services.AddSingleton<IChainStoreBundle>(_ => bundle);
            return services.BuildServiceProvider();
        }
    }
}
