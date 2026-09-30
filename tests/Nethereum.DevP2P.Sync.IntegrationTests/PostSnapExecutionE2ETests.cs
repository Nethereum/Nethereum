using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class PostSnapExecutionE2ETests
    {
        private readonly ITestOutputHelper _output;

        public PostSnapExecutionE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_ASnapSyncedNode_When_ItExecutesForwardFromThePivot_Then_ItReachesTheProducersHead()
        {
            var producerNode = DevChainNode.CreateWithRocksDb();
            var joiner = DevChainNode.CreateWithRocksDb();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);
                var load = await DevChainLoad.GenerateAsync(producerNode, producer, blocks: 80, contracts: 2);
                var producerHeight = await producer.HeightAsync();
                _output.WriteLine($"producer at {producerHeight}");

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var follower = await DevChainFollower.AttachAsync(joiner);
                var log = new CapturingLogger();
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    var snap = await follower.SnapBootstrapAsync(deadline.Token);
                    Assert.True(snap.Ran, $"snap did not run: {snap.SkipReason}");
                    _output.WriteLine($"snap complete at pivot {snap.PivotBlockNumber}; executed={follower.ExecutedHeight}");

                    await follower.StartCatchUpAsync(log);

                    var reached = await DevChainNetwork.WaitUntilAsync(
                        () => follower.ExecutedHeight >= producerHeight,
                        TimeSpan.FromSeconds(90));

                    foreach (var line in log.Lines.Take(30)) _output.WriteLine($"  {line}");

                    Assert.True(reached,
                        $"snap-synced node executed only to {follower.ExecutedHeight} of {producerHeight}");

                    var mine = await joiner.Bundle.Blocks.GetHashByNumberAsync(producerHeight);
                    var theirs = await producerNode.Bundle.Blocks.GetHashByNumberAsync(producerHeight);
                    Assert.True(ByteUtil.AreEqual(mine, theirs), "head block hash differs from the producer's");

                    _output.WriteLine($"snap-synced node executed forward to {producerHeight}");
                }
                finally
                {
                    await follower.DisposeAsync();
                }
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly List<string> _lines = new List<string>();
            private readonly object _gate = new object();

            public IReadOnlyList<string> Lines
            {
                get { lock (_gate) return _lines.ToList(); }
            }

            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var line = $"[{logLevel}] {formatter(state, exception)}";
                if (exception != null) line += $" EX: {exception.Message}";
                lock (_gate) _lines.Add(line);
            }
        }
    }
}
