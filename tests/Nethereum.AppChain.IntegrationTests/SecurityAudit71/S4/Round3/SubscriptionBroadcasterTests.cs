using System.Diagnostics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class SubscriptionBroadcasterTests
    {
        private static BlockHeader Header(int number) => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
            BlockNumber = number,
            GasLimit = 30_000_000,
            Timestamp = number,
        };

        private static WebSocketRpcHandler NewHandler() =>
            new WebSocketRpcHandler(new SubscriptionManager(), new RpcHandlerRegistry(), new RpcContext((Func<IChainNode>)(() => null!), 1, null!));

        [Fact]
        [Trait("Finding", "S4-R3-03")]
        public async Task Given_TheFanOutIsStuck_When_ManyBlocksAreHandedOver_Then_EveryHandOverReturnsAtOnceAndTheQueueKeepsOnlyTheNewest()
        {
            var gate = new TaskCompletionSource();
            var seen = new List<long>();
            var failures = 0;
            using var broadcaster = new SubscriptionBroadcaster(
                NewHandler(),
                async header =>
                {
                    lock (seen) seen.Add((long)header.BlockNumber);
                    await gate.Task;
                    return new List<FilteredLog>();
                },
                (_, _) => Interlocked.Increment(ref failures));

            var clock = Stopwatch.StartNew();
            for (var i = 1; i <= 1000; i++) broadcaster.Enqueue(Header(i), new byte[32]);
            var handOver = clock.ElapsedMilliseconds;

            gate.SetResult();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                lock (seen) if (seen.Contains(1000)) break;
                await Task.Delay(20);
            }

            Assert.True(handOver < 500, "handing over 1000 blocks took " + handOver + " ms while the fan-out was stuck");
            lock (seen)
            {
                Assert.True(seen.Count <= SubscriptionBroadcaster.QueuedBlocks + 1, "the queue kept " + seen.Count + " blocks");
                Assert.Contains(1000L, seen);
            }
            Assert.Equal(0, failures);
        }
    }
}
