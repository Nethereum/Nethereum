using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;

namespace Nethereum.MainnetChain.UnitTests
{
    public sealed class FatalProbeException : Exception
    {
        public FatalProbeException() : base("follower stack probe failure") { }
    }

    public sealed class RecordingLogger : ILogger<MainnetChainHostedService>
    {
        public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

        private readonly ConcurrentQueue<Entry> _entries = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _entries.Enqueue(new Entry(logLevel, formatter(state, exception), exception));

        public IReadOnlyList<Entry> Entries => _entries.ToArray();
    }

    public class ForwardingProxy<T> : DispatchProxy where T : class
    {
        public T Target { get; set; } = null!;

        public Func<MethodInfo, object?[]?, (bool Handled, object? Result)>? Hook { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;
            if (Hook != null)
            {
                var hooked = Hook(targetMethod, args);
                if (hooked.Handled) return hooked.Result;
            }
            try
            {
                return targetMethod.Invoke(Target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        public static T Wrap(T target, Func<MethodInfo, object?[]?, (bool Handled, object? Result)> hook)
        {
            var proxy = Create<T, ForwardingProxy<T>>();
            var typed = (ForwardingProxy<T>)(object)proxy;
            typed.Target = target;
            typed.Hook = hook;
            return proxy;
        }
    }

    public static class HostedServiceTestSupport
    {
        public static IChainStoreBundle BundleWhoseMetadataThrows(
            InMemoryChainStoreBundle inner,
            Action<MethodInfo> onMetadataRead,
            Action? onDisposed = null,
            params string[] throwingMetadataMethods)
        {
            var throwing = new HashSet<string>(throwingMetadataMethods);
            var metadata = ForwardingProxy<IChainMetadataStore>.Wrap(inner.Metadata, (method, _) =>
            {
                onMetadataRead(method);
                if (throwing.Contains(method.Name)) throw new FatalProbeException();
                return (false, null);
            });
            return ForwardingProxy<IChainStoreBundle>.Wrap(inner, (method, _) =>
            {
                if (method.Name == "get_Metadata") return (true, metadata);
                if (method.Name == "Dispose" || method.Name == "DisposeAsync") onDisposed?.Invoke();
                return (false, null);
            });
        }

        public static ServiceProvider BuildProvider(IChainStoreBundle? bundleOverride)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.UseInMemoryBundleAndSource(new EmptyBlockSource());
            services.AddMainnetChainServer(new MainnetChainServerConfig { DataDir = null });
            if (bundleOverride != null)
                services.AddSingleton<IChainStoreBundle>(_ => bundleOverride);
            return services.BuildServiceProvider();
        }

        public static MainnetChainHostedService CreateService(
            IServiceProvider provider, ILogger<MainnetChainHostedService> logger, HeaderFollowService? headerFollow = null)
        {
            var mapped = new MainnetChainServerConfig { DataDir = null }.ToChainNodeConfig();
#pragma warning disable CS0618
            return new MainnetChainHostedService(
                provider.GetRequiredService<MainnetChainNodeFactory>(),
                provider.GetRequiredService<IChainStoreBundle>(),
                provider.GetRequiredService<IBlockSource>(),
                provider.GetRequiredService<IValidationPolicy>(),
                provider.GetRequiredService<MainnetChainServerConfig>(),
                logger,
                provider.GetRequiredService<MainnetChainNodeAccessor>(),
                null,
                null,
                null,
                null,
                headerFollow,
                null,
                null,
                mapped);
#pragma warning restore CS0618
        }

        private sealed class EmptyBlockSource : IBlockSource
        {
            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(default(BlockSourceHealth));

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal? LastChainBreak => null;
        }
    }
}
