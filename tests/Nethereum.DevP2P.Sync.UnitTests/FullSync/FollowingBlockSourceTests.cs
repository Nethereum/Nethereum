using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.FullSync
{
    public class FollowingBlockSourceTests
    {
        private sealed class NoHandshake : IPeerHandshakeWorker
        {
            public Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct) =>
                throw new NotSupportedException();
        }

        private sealed class GrowingChainSource : IBlockSource
        {
            private long _head;
            public readonly List<ulong> Requested = new List<ulong>();
            public DivergenceSignal Break { get; set; }

            public DivergenceSignal LastChainBreak => Break;

            public void Produce() => Interlocked.Increment(ref _head);

            public int RequestCount { get { lock (Requested) return Requested.Count; } }

            public bool HasRequested(ulong from) { lock (Requested) return Requested.Contains(from); }

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                lock (Requested) Requested.Add(fromBlock);
                for (var number = fromBlock; number <= (ulong)Interlocked.Read(ref _head); number++)
                {
                    yield return new BlockBundle(
                        new BlockHeader { BlockNumber = (long)number },
                        new List<ISignedTransaction>(), new List<BlockHeader>(), null, new byte[32]);
                }
                await Task.CompletedTask;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) =>
                Task.CompletedTask;
        }

        private static PeerPoolManager PoolWithNoPeers() =>
            new PeerPoolManager(new NoHandshake(), new PeerPoolOptions(TargetPeerCount: 1, MaxConcurrentDials: 1));

        private static int SeenCount(List<ulong> seen) { lock (seen) return seen.Count; }

        [Fact]
        public async Task Given_ThePullSourceHasRunOut_When_ABlockIsProducedLater_Then_TheStreamKeepsPullingAndYieldsItWithoutEnding()
        {
            var pull = new GrowingChainSource();
            await using var pool = PoolWithNoPeers();
            var source = new FollowingBlockSource(pull, pool, _ => Task.FromResult<byte[]>(null), fallbackPoll: TimeSpan.FromMilliseconds(20));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var seen = new List<ulong>();

            var stream = Task.Run(async () =>
            {
                await foreach (var bundle in source.StreamAsync(1, cts.Token))
                {
                    lock (seen) seen.Add((ulong)bundle.Header.BlockNumber);
                    if (SeenCount(seen) == 2) cts.Cancel();
                }
            });

            Assert.True(await SnapLoopback.WaitUntilAsync(() => pull.RequestCount >= 2, TimeSpan.FromSeconds(60)),
                "the stream never pulled again after the source ran out");
            pull.Produce();
            Assert.True(await SnapLoopback.WaitUntilAsync(() => SeenCount(seen) == 1 && pull.HasRequested(2), TimeSpan.FromSeconds(60)),
                "the stream never pulled from block 2 after yielding the first produced block");
            pull.Produce();

            await stream;

            Assert.Equal(new ulong[] { 1, 2 }, seen);
            lock (pull.Requested) Assert.Contains(2UL, pull.Requested);
        }

        [Fact]
        public async Task Given_ThePullSourceReportsAChainBreak_When_ItRunsOut_Then_TheStreamEndsSoTheFollowerCanHandleTheBreak()
        {
            var pull = new GrowingChainSource
            {
                Break = new DivergenceSignal(5, new byte[32], new byte[32], 1, "pull")
            };
            await using var pool = PoolWithNoPeers();
            var source = new FollowingBlockSource(pull, pool, _ => Task.FromResult<byte[]>(null), fallbackPoll: TimeSpan.FromMilliseconds(20));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await foreach (var yielded in source.StreamAsync(5, cts.Token)) { Assert.Null(yielded); }

            Assert.Same(pull.Break, source.LastChainBreak);
            Assert.False(cts.IsCancellationRequested);
        }
    }
}
