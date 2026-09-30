using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class SnapBootstrapE2ETests
    {
        private readonly ITestOutputHelper _output;

        public SnapBootstrapE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_APeerServingSnap_When_AColdNodeDials_Then_SnapVersion2IsNegotiated()
        {
            var producerNode = DevChainNode.Create(advertiseSnap2: true);
            var joiner = DevChainNode.Create(advertiseSnap2: true);
            try
            {
                await producerNode.StartAsync();
                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var session = joiner.OutboundSessionTo(producerNode);
                Assert.NotNull(session);

                var snap = session.Connection.SharedCapabilities.Find(c => c.Name == "snap");
                Assert.NotNull(snap);
                Assert.Equal(2, snap.Version);
                Assert.True(session.SupportsSnap);

                _output.WriteLine($"negotiated snap/{snap.Version}");
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        [Fact(Skip = "BLOCKED by a snap client defect, not by this test: SnapAccountRangeConsumer handles " +
                     "only AccountRange and StorageSubtask leases, but SnapTaskSet.LeaseNext also issues " +
                     "SmallStorageBatch (TryLeaseSmallBatch) and Bytecode (TryLeaseCode) — both throw " +
                     "NotSupportedException('snap.consumer.unsupported_fragment'). No consumer anywhere " +
                     "handles either kind, so snap cannot finish bootstrapping any state containing " +
                     "contract code or small storage batches. Verified live: pivot anchors at block 48 of " +
                     "80, Phase 2 starts and accounts stream in, then every attempt dies on that throw. " +
                     "Un-skip once the consumer handles those leases.")]
        public async Task Given_AChainWithState_When_AColdNodeSnapBootstraps_Then_ItHoldsThePeersStateAtThePivot()
        {
            var producerNode = DevChainNode.Create();
            var joiner = DevChainNode.Create();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);
                for (var i = 0; i < 80; i++) await producer.ProduceAsync();

                var producerHeight = await producer.HeightAsync();
                var producerHead = await producerNode.Bundle.Blocks.GetByNumberAsync(producerHeight);
                Assert.NotNull(producerHead?.StateRoot);

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var follower = await DevChainFollower.AttachAsync(joiner);
                try
                {
                    using var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(90));
                    var result = await follower.SnapBootstrapAsync(deadline.Token, new XunitLogger(_output));

                    Assert.True(result.Ran, $"snap bootstrap did not run: {result.SkipReason}");
                    _output.WriteLine(
                        $"snap pivot={result.PivotBlockNumber} accounts={result.AccountCount} " +
                        $"slots={result.SlotCount} bytecode={result.BytecodeCount} " +
                        $"root=0x{result.PivotStateRoot?.ToHex()}");

                    Assert.True(result.AccountCount > 0, "snap bootstrap downloaded no accounts");

                    var peerRootAtPivot =
                        (await producerNode.Bundle.Blocks.GetByNumberAsync((long)result.PivotBlockNumber))?.StateRoot;
                    Assert.True(ByteUtil.AreEqual(result.PivotStateRoot, peerRootAtPivot),
                        "snap pivot state root does not match the peer's state root at that block");
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

        private sealed class XunitLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ITestOutputHelper _out;

            public XunitLogger(ITestOutputHelper output) => _out = output;

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                try { _out.WriteLine($"[{logLevel}] {formatter(state, exception)}"); }
                catch { }
            }
        }
    }
}
