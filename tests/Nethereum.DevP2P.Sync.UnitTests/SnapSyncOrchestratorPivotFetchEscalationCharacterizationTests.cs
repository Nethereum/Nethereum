using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncOrchestratorPivotFetchEscalationCharacterizationTests
    {
        [Fact]
        public async Task Given_SixConsecutiveEmptyHeaderResponses_When_ThePivotIsFetched_Then_TheStallIsLoggedWithTheFailureCountAndLastReason()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            using var cts = new CancellationTokenSource();
            var log = new CharLog();
            var calls = 0;
            var scheduler = new CharScheduler
            {
                Headers = (start, limit, ct) =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new List<BlockHeader>());
                },
            };
            var tip = new Nethereum.CoreChain.Validation.CanonicalTip { BlockNumber = 1_000 };
            var run = SnapSyncOrchestrator.RunAsync(
                bundle, new CharPool(), scheduler, new CharTipSource(tip), activations: null, log,
                new SnapSyncOrchestratorOptions { UseBackwardSkeleton = false, RunHistoryBackfill = false }, cts.Token);

            Assert.True(await log.WaitForAsync("pivot fetch stalled", TimeSpan.FromSeconds(40)));
            cts.Cancel();
            var result = await run;

            var stalled = Assert.Single(log.With("pivot fetch stalled"));
            Assert.Equal(LogLevel.Warning, stalled.Level);
            Assert.Equal(
                $"Snap-bootstrap: pivot fetch stalled after 6 failures (last: empty_response); waiting {SnapSyncOrchestrator.PivotFetchEscalationDelayMs}ms",
                stalled.Message);
            Assert.Equal(6, calls);
            Assert.False(result.Ran);
            Assert.Equal("cancelled while fetching pivot", result.SkipReason);
        }
    }
}
