using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection(HostedServiceProcessExitCodeCollection.Name)]
    public class MainnetChainHostedServiceHostTests
    {
        private const string HostCategory = "Microsoft.Extensions.Hosting.Internal.Host";

        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

        private sealed record Captured(string Category, LogLevel Level, string Message, Exception? Exception);

        private sealed class CapturingProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<Captured> _entries = new();

            public IReadOnlyList<Captured> Entries => _entries.ToArray();

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

            public void Dispose() { }

            private sealed class CapturingLogger : ILogger
            {
                private readonly string _category;
                private readonly ConcurrentQueue<Captured> _sink;

                public CapturingLogger(string category, ConcurrentQueue<Captured> sink)
                {
                    _category = category;
                    _sink = sink;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                    => _sink.Enqueue(new Captured(_category, logLevel, formatter(state, exception), exception));
            }
        }

        [Fact]
        public async Task Given_AHostWhoseFollowerFaultsAfterStart_When_ItRuns_Then_TheOutputHasACriticalFollowerFatalLineAndTheExitCodeIsOne()
        {
            var savedExitCode = Environment.ExitCode;
            try
            {
                Environment.ExitCode = 0;
                using var inner = InMemoryChainStoreBundle.Open();
                var bundle = HostedServiceTestSupport.BundleWhoseMetadataThrows(
                    inner, _ => { }, null, "GetSnapSyncState");
                await using var dependencies = HostedServiceTestSupport.BuildProvider(bundle);

                var capture = new CapturingProvider();
                var builder = Host.CreateApplicationBuilder();
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(capture);
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                builder.Logging.AddFilter("Nethereum", LogLevel.Information);
                builder.Logging.AddFilter(HostCategory, LogLevel.Critical);
                builder.Services.AddSingleton<IHostedService>(sp =>
                    HostedServiceTestSupport.CreateService(
                        dependencies, sp.GetRequiredService<ILogger<MainnetChainHostedService>>()));

                using var host = builder.Build();
                try { await host.RunAsync().WaitAsync(Bound); }
                catch (FatalProbeException) { }

                var fatal = Assert.Single(capture.Entries, e => e.Level == LogLevel.Critical && e.Message.Contains("follower.fatal"));
                Assert.IsType<FatalProbeException>(fatal.Exception);
                Assert.Contains(capture.Entries, e => e.Category == HostCategory && e.Level == LogLevel.Critical);
                Assert.Equal(1, Environment.ExitCode);
            }
            finally
            {
                Environment.ExitCode = savedExitCode;
            }
        }
    }
}
