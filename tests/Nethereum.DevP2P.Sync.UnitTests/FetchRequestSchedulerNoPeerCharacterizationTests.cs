using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerNoPeerCharacterizationTests
    {
        [Fact]
        public async Task Given_AnEmptyPeerPool_When_TheCallerCancelsWhileAwaitingAPeer_Then_TheRequestFailsWithTheNoPeerAvailableCauseInsteadOfACancellation()
        {
            var scheduler = new FetchRequestScheduler(new CharPool(), new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var thrown = await Record.ExceptionAsync(() => scheduler.FetchHeadersAsync(1, 1, cts.Token));

            if (thrown is not FetchRequestFailedException failed)
            {
                Assert.IsAssignableFrom<OperationCanceledException>(thrown);
                return;
            }
            Assert.EndsWith("failed after 0 attempts across 0 peers.", failed.Message);
            var cause = Assert.IsType<InvalidOperationException>(failed.InnerException);
            Assert.EndsWith("after 0 attempts.", cause.Message);
            Assert.StartsWith("No peer available for ", cause.Message);
        }

        [Fact]
        public async Task Given_AnEmptyPeerPool_When_TheCallerIsAlreadyCancelled_Then_TheCancellationPropagatesWithoutWaitingForAPeer()
        {
            var scheduler = new FetchRequestScheduler(new CharPool(), new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => scheduler.FetchHeadersAsync(1, 1, cts.Token));
        }
    }
}
