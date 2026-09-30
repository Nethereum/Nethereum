using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class DiscoveryLoopTests
    {
        private static readonly TimeSpan NoWait = TimeSpan.Zero;

        [Fact]
        public async Task Given_AHarvestReturnsEnodes_When_TheLoopRuns_Then_EveryEnodeIsEnqueued()
        {
            using var cts = new CancellationTokenSource();
            var enqueued = new List<string>();
            int harvests = 0;

            await DiscoveryLoop.RunAsync(
                harvest: _ =>
                {
                    harvests++;
                    cts.Cancel();
                    return Task.FromResult((IReadOnlyList<string>)new[] { "enode://a@1.2.3.4:30303", "enode://b@5.6.7.8:30303" });
                },
                enqueue: enqueued.Add,
                interval: NoWait,
                onError: _ => { },
                ct: cts.Token);

            Assert.Equal(new[] { "enode://a@1.2.3.4:30303", "enode://b@5.6.7.8:30303" }, enqueued);
            Assert.Equal(1, harvests);
        }

        [Fact]
        public async Task Given_AHarvestThrows_When_TheLoopRuns_Then_ItIsReportedAndTheLoopContinues()
        {
            using var cts = new CancellationTokenSource();
            var enqueued = new List<string>();
            int harvests = 0, errors = 0;

            await DiscoveryLoop.RunAsync(
                harvest: _ =>
                {
                    harvests++;
                    if (harvests == 1) throw new InvalidOperationException("bootnode unreachable this round");
                    cts.Cancel();
                    return Task.FromResult((IReadOnlyList<string>)new[] { "enode://c@9.9.9.9:30303" });
                },
                enqueue: enqueued.Add,
                interval: NoWait,
                onError: _ => errors++,
                ct: cts.Token);

            Assert.Equal(1, errors);
            Assert.Equal(new[] { "enode://c@9.9.9.9:30303" }, enqueued);
            Assert.Equal(2, harvests);
        }

        [Fact]
        public async Task Given_AHarvestReturnsNull_When_TheLoopRuns_Then_NothingIsEnqueuedAndItIsNotAnError()
        {
            using var cts = new CancellationTokenSource();
            var enqueued = new List<string>();
            int harvests = 0, errors = 0;

            await DiscoveryLoop.RunAsync(
                harvest: _ => { harvests++; cts.Cancel(); return Task.FromResult<IReadOnlyList<string>>(null); },
                enqueue: enqueued.Add,
                interval: NoWait,
                onError: _ => errors++,
                ct: cts.Token);

            Assert.Empty(enqueued);
            Assert.Equal(0, errors);
            Assert.Equal(1, harvests);
        }

        [Fact]
        public async Task Given_APreCancelledToken_When_TheLoopStarts_Then_ItExitsWithoutHarvesting()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            int harvests = 0;

            await DiscoveryLoop.RunAsync(
                harvest: _ => { harvests++; return Task.FromResult((IReadOnlyList<string>)Array.Empty<string>()); },
                enqueue: _ => { },
                interval: NoWait,
                onError: _ => { },
                ct: cts.Token);

            Assert.Equal(0, harvests);
        }

        [Fact]
        public async Task Given_TheHarvestItselfHonoursCancellation_When_ItThrowsOperationCanceled_Then_TheLoopStopsQuietly()
        {
            using var cts = new CancellationTokenSource();
            int errors = 0;

            await DiscoveryLoop.RunAsync(
                harvest: token =>
                {
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
                },
                enqueue: _ => { },
                interval: NoWait,
                onError: _ => errors++,
                ct: cts.Token);

            Assert.Equal(0, errors);
        }
    }
}
