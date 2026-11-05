using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    public class S2CoverageTests
    {
        [Fact]
        public async Task S2_A2_Given_APeerServesASelfConsistentChainWhoseTopHashIsNotTheAnchor_When_TheBackwardWalkRuns_Then_ThePeerIsQuarantinedAndTheAnchoredChainIsStored()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var real = new List<BlockHeader>();
            var forged = new List<BlockHeader>();
            var prevReal = new byte[32];
            var prevForged = new byte[32];
            for (long n = 0; n < 20; n++)
            {
                var header = S2Chain.HeaderAt(n, prevReal);
                real.Add(header);
                prevReal = S2Chain.HashOf(header);

                var fake = S2Chain.HeaderAt(n, prevForged);
                fake.ExtraData = new byte[] { 0xEE };
                forged.Add(fake);
                prevForged = S2Chain.HashOf(fake);
            }

            var liar = new S2Peer(19);
            var honest = new S2Peer(19);
            var pool = new S2Pool(liar, honest);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) =>
                {
                    var source = peer.Id == liar.Id ? forged : real;
                    var batch = new List<BlockHeader>();
                    for (long n = (long)start; n >= 0 && batch.Count < (long)limit; n--) batch.Add(source[(int)n]);
                    return Task.FromResult(batch);
                }
            };
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions())
            {
                HeaderBatchQuarantineDuration = TimeSpan.FromSeconds(30)
            };
            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 5,
                    MaxAnchorRetries = 5,
                    PeerRetryDelay = TimeSpan.Zero,
                    HeadersOnly = true
                },
                NullLogger<BackwardBlockWalker>.Instance);
            var anchor = S2Chain.HashOf(real[19]);

            var result = await walker.WalkAsync(
                19, anchor, 0,
                (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(anchor, await bundle.Blocks.GetHashByNumberAsync(new BigInteger(19)));
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task S2_R9_01_Given_AClaimantThatAnswersEveryHeaderRequestEmptyWhileAnHonestPeerServes_When_TheTipIsFetchedTenTimes_Then_TheFalseClaimantIsAskedAtMostTwice()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(100);
            var pool = new S2Pool(liar, honest);
            var header = S2Chain.HeaderAt(100, new byte[32]);
            var worker = new S2Worker
            {
                Headers = async (peer, start, limit, reverse, ct) =>
                {
                    if (peer.Id == liar.Id) return new List<BlockHeader>();
                    await Task.Delay(20, ct);
                    return new List<BlockHeader> { header };
                }
            };
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            for (var i = 0; i < 10; i++)
                await scheduler.FetchAnnouncedHeadersAsync(100, 8, CancellationToken.None);

            Assert.True(
                worker.CallsOf(liar.Id, "headers") <= 2,
                $"the false claimant was asked {worker.CallsOf(liar.Id, "headers")} times in 10 fetches");
        }

        private sealed class BanningHandshake : IPeerHandshakeWorker
        {
            public Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => throw new SyncPeerSession.UselessPeerException("behind");
        }

        [Fact]
        public async Task S2_E3_Given_ABannedCandidateAndAMinuteOfPoolLife_When_ABanRetentionHasPassedAndTheDialLoopTurns_Then_TheBanEntryIsSwept()
        {
            var pool = new PeerPoolManager(new BanningHandshake(), new PeerPoolOptions(TargetPeerCount: 4, DialBudgetPerSecond: 0));
            var enode = $"enode://{new string('d', 128)}@127.0.0.1:39001";
            await pool.StartAsync(CancellationToken.None);
            try
            {
                pool.EnqueueCandidate(enode);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!pool.IsBannedForTest(enode) && DateTime.UtcNow < deadline) await Task.Delay(50);
                Assert.True(pool.IsBannedForTest(enode), "the useless candidate was never banned");

                await Task.Delay(TimeSpan.FromSeconds(62));
                pool.EnqueueCandidate($"enode://{new string('e', 128)}@127.0.0.1:39002");
                deadline = DateTime.UtcNow.AddSeconds(15);
                while (pool.IsBannedForTest(enode) && DateTime.UtcNow < deadline) await Task.Delay(50);

                Assert.False(pool.IsBannedForTest(enode), "the ban entry outlived its retention and the sweep interval");
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }
    }
}
