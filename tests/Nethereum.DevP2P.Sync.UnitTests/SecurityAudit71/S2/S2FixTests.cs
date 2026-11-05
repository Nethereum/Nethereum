using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    public class S2FixTests
    {
        private const long Cursor = 100;
        private static readonly byte[] Parent = Enumerable.Repeat((byte)0x11, 32).ToArray();

        private static FetchRequestScheduler SchedulerOf(S2Pool pool, S2Worker worker)
            => new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

        private static List<BlockBody> BodiesFor(IReadOnlyList<byte[]> hashes)
            => hashes.Select(_ => S2Chain.EmptyBody()).ToList();

        [Fact]
        public async Task S2_01_Given_APeerLacksTheBodiesAndAnotherServesThem_When_TheFollowerStreams_Then_TheLaggardIsNotQuarantinedAndTheSyncCompletes()
        {
            var thin = new S2Peer(Cursor + 50);
            var full = new S2Peer(Cursor + 50);
            var pool = new S2Pool(thin, full);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) => Task.FromResult(new List<BlockHeader> { header }),
                Bodies = (peer, hashes, ct) => Task.FromResult(peer.Id == thin.Id ? new List<BlockBody>() : BodiesFor(hashes))
            };
            var scheduler = SchedulerOf(pool, worker);
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            BlockBundle? first = null;
            await foreach (var bundle in source.StreamAsync((ulong)Cursor, cts.Token))
            {
                first = bundle;
                break;
            }

            Assert.NotNull(first);
            Assert.False(scheduler.IsHeaderPeerQuarantined(thin.Id));
            Assert.False(scheduler.IsHeaderPeerQuarantined(full.Id));
        }

        [Fact]
        public async Task S2_01_Given_ADemotedPeerIsTheOnlyOneLeftAndNowHoldsTheBodies_When_BodiesAreFetched_Then_ItIsStillAskedAsALastResort()
        {
            var thin = new S2Peer(Cursor + 50);
            var full = new S2Peer(Cursor + 50);
            var pool = new S2Pool(thin, full);
            var thinHasBodies = false;
            var worker = new S2Worker
            {
                Bodies = (peer, hashes, ct) => Task.FromResult(
                    peer.Id == thin.Id && !thinHasBodies ? new List<BlockBody>() : BodiesFor(hashes))
            };
            var scheduler = SchedulerOf(pool, worker);
            var hashes = new List<byte[]> { new byte[32] };

            var firstRound = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);
            await pool.DropAsync(full.Id, "gone", CancellationToken.None);
            thinHasBodies = true;
            var secondRound = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);

            Assert.Single(firstRound);
            Assert.Single(secondRound);
        }

        [Fact]
        public async Task S2_01_Given_EveryPeerAnswersTheBodyRequestEmpty_When_BodiesAreFetched_Then_TheCallReturnsEmptyWithoutWaiting()
        {
            var a = new S2Peer(Cursor + 50);
            var b = new S2Peer(Cursor + 50);
            var pool = new S2Pool(a, b);
            var worker = new S2Worker();
            var scheduler = SchedulerOf(pool, worker);

            var bodies = await scheduler.FetchBodiesAsync(new List<byte[]> { new byte[32] }, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(bodies);
            Assert.Equal(1, worker.CallsOf(a.Id, "bodies"));
            Assert.Equal(1, worker.CallsOf(b.Id, "bodies"));
        }

        [Fact]
        public async Task S2_01_Given_AnHonestPeerSlowerThanTheDefaultForUnknownPeers_When_BodiesAreFetchedThreeTimes_Then_TheEmptyAnsweringPeerIsAskedOnlyOnce()
        {
            var liar = new S2Peer(Cursor + 50);
            var slow = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, slow);
            var worker = new S2Worker
            {
                Bodies = async (peer, hashes, ct) =>
                {
                    if (peer.Id == liar.Id) return new List<BlockBody>();
                    await Task.Delay(1100, ct);
                    return BodiesFor(hashes);
                }
            };
            var scheduler = SchedulerOf(pool, worker);

            for (var i = 0; i < 3; i++)
                await scheduler.FetchBodiesAsync(new List<byte[]> { new byte[32] }, CancellationToken.None);

            Assert.Equal(1, worker.CallsOf(liar.Id, "bodies"));
            Assert.Equal(3, worker.CallsOf(slow.Id, "bodies"));
        }

        [Fact]
        public async Task S2_01_Given_EveryPeerAnswersEmptyAtTheTip_When_TheTipIsPolledTwice_Then_NoPeerIsDemotedAndTheClaimantIsStillAskedFirst()
        {
            var other = new S2Peer(Cursor - 1);
            var claimant = new S2Peer(Cursor + 50);
            var pool = new S2Pool(other, claimant);
            var asked = new List<Guid>();
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) =>
                {
                    lock (asked) asked.Add(peer.Id);
                    return Task.FromResult(new List<BlockHeader>());
                }
            };
            var scheduler = SchedulerOf(pool, worker);

            await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);
            await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);

            Assert.Equal(new[] { claimant.Id, other.Id, claimant.Id, other.Id }, asked);
        }

        [Fact]
        public async Task S2_01_Given_AClaimantAnswersEmptyWhileAnotherPeerServes_When_TheTipIsPolled_Then_TheClaimantIsDemotedForTheNextPoll()
        {
            var other = new S2Peer(Cursor - 1);
            var claimant = new S2Peer(Cursor + 50);
            var pool = new S2Pool(other, claimant);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var asked = new List<Guid>();
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) =>
                {
                    lock (asked) asked.Add(peer.Id);
                    return Task.FromResult(peer.Id == other.Id ? new List<BlockHeader> { header } : new List<BlockHeader>());
                }
            };
            var scheduler = SchedulerOf(pool, worker);

            await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);
            await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);

            Assert.Equal(new[] { claimant.Id, other.Id, other.Id }, asked);
        }

        [Fact]
        public async Task S2_01_Given_AFastPeerAnswersTheReceiptRequestEmpty_When_ReceiptsAreFetched_Then_TheNextPeerServesThem()
        {
            var liar = new S2Peer(Cursor + 50);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var worker = new S2Worker
            {
                Receipts = (peer, hashes, ct) => Task.FromResult(
                    peer.Id == liar.Id ? new List<List<Receipt>>() : hashes.Select(_ => new List<Receipt>()).ToList())
            };
            var scheduler = SchedulerOf(pool, worker);

            var receipts = await scheduler.FetchReceiptsAsync(new List<byte[]> { new byte[32] }, CancellationToken.None);

            Assert.Single(receipts);
        }

        [Fact]
        public async Task S2_01_Given_APeerServesOnlyAOneBlockPrefixOfEachBodyRequest_When_BodiesAreFetchedRepeatedly_Then_ThePrefixIsAcceptedAndThePeerIsNotDemoted()
        {
            var thin = new S2Peer(Cursor + 50);
            var other = new S2Peer(Cursor + 50);
            var pool = new S2Pool(thin, other);
            var worker = new S2Worker
            {
                Bodies = (peer, hashes, ct) => Task.FromResult(BodiesFor(peer.Id == thin.Id ? hashes.Take(1).ToList() : hashes))
            };
            var scheduler = SchedulerOf(pool, worker);
            var hashes = Enumerable.Range(0, 8).Select(_ => new byte[32]).ToList();

            for (var i = 0; i < 5; i++)
            {
                var bodies = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);
                Assert.True(bodies.Count >= 1);
            }

            Assert.Equal(5, worker.CallsOf(thin.Id, "bodies") + worker.CallsOf(other.Id, "bodies"));
        }

        [Fact]
        public async Task S2_01_Given_ALiarAnswersEmptyAndAnHonestPeerServes_When_HeadersAreFetchedTenTimes_Then_TheHonestPeerIsAskedEveryTimeAndNeverQuarantined()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = async (peer, start, limit, reverse, ct) =>
                {
                    if (peer.Id == liar.Id) return new List<BlockHeader>();
                    await Task.Delay(10, ct);
                    return new List<BlockHeader> { header };
                }
            };
            var scheduler = SchedulerOf(pool, worker);

            for (var i = 0; i < 10; i++)
                await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);

            Assert.Equal(10, worker.CallsOf(honest.Id, "headers"));
            Assert.False(scheduler.IsHeaderPeerQuarantined(honest.Id));
        }

        [Fact]
        public async Task S2_02_Given_APeerAnswersWrongNumbersAndAnHonestPeerServes_When_HeadersAreFetchedRepeatedly_Then_OnlyTheLiarIsQuarantined()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var wrong = S2Chain.HeaderAt(Cursor + 7, Parent);
            var right = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) => Task.FromResult(
                    new List<BlockHeader> { peer.Id == liar.Id ? wrong : right })
            };
            var scheduler = SchedulerOf(pool, worker);

            for (var i = 0; i < 5; i++)
                await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 8, CancellationToken.None);

            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
            Assert.False(scheduler.IsHeaderPeerQuarantined(honest.Id));
            Assert.Equal(1, worker.CallsOf(liar.Id, "headers"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task S2_02_Given_AReplyBreaksTheNumberingRules_When_HeadersAreFetched_Then_ThePeerIsQuarantined(bool reverse)
        {
            var liar = new S2Peer(Cursor + 50);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var first = S2Chain.HeaderAt(Cursor, Parent);
            var gapped = S2Chain.HeaderAt(Cursor + (reverse ? -2 : 2), Parent);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, rev, ct) => Task.FromResult(peer.Id == liar.Id
                    ? new List<BlockHeader> { first, gapped }
                    : new List<BlockHeader> { first })
            };
            var scheduler = SchedulerOf(pool, worker);

            var headers = await scheduler.FetchHeadersAsync((ulong)Cursor, 8, CancellationToken.None, reverse);

            Assert.Single(headers);
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task S2_02_Given_APeerAnswersMoreHeadersThanRequested_When_HeadersAreFetched_Then_ThePeerIsQuarantined()
        {
            var liar = new S2Peer(Cursor + 50);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var chain = Enumerable.Range(0, 3).Select(i => S2Chain.HeaderAt(Cursor + i, Parent)).ToList();
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, rev, ct) => Task.FromResult(peer.Id == liar.Id ? chain : chain.Take(2).ToList())
            };
            var scheduler = SchedulerOf(pool, worker);

            var headers = await scheduler.FetchHeadersAsync((ulong)Cursor, 2, CancellationToken.None);

            Assert.Equal(2, headers.Count);
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task S2_02_Given_AnHonestPeerOnAReorgedChainWhoseParentIsNotOurs_When_TheFollowerStreams_Then_ItIsAChainBreakAndThePeerIsNotQuarantined()
        {
            var peer = new S2Peer(Cursor + 50);
            var pool = new S2Pool(peer);
            var reorged = S2Chain.HeaderAt(Cursor, Enumerable.Repeat((byte)0x22, 32).ToArray());
            var worker = new S2Worker
            {
                Headers = (p, start, limit, reverse, ct) => Task.FromResult(new List<BlockHeader> { reorged }),
                Bodies = (p, hashes, ct) => Task.FromResult(BodiesFor(hashes))
            };
            var scheduler = SchedulerOf(pool, worker);
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var bundle in source.StreamAsync((ulong)Cursor, cts.Token)) Assert.Null(bundle);

            Assert.NotNull(source.LastChainBreak);
            Assert.False(scheduler.IsHeaderPeerQuarantined(peer.Id));
        }

        [Fact]
        public async Task S2_01_Given_EveryPeerAnswersEveryBodyRequestEmpty_When_TheFollowerStreams_Then_TheStreamEndsAfterOnePassInsteadOfLooping()
        {
            var peer = new S2Peer(Cursor + 50);
            var pool = new S2Pool(peer);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = (p, start, limit, reverse, ct) => Task.FromResult(new List<BlockHeader> { header }),
                Bodies = (p, hashes, ct) => Task.FromResult(new List<BlockBody>())
            };
            var scheduler = SchedulerOf(pool, worker);
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var yielded = 0;
            await foreach (var bundle in source.StreamAsync((ulong)Cursor, cts.Token)) yielded++;
            await Task.Delay(500);

            Assert.Equal(0, yielded);
            Assert.False(cts.IsCancellationRequested);
            Assert.Equal(1, worker.CallsOf(peer.Id, "bodies"));
            Assert.Equal(1, worker.CallsOf(peer.Id, "headers"));
        }

        private sealed class PassSource : IBlockSource
        {
            public int Passes;
            public long Head;

            public DivergenceSignal LastChainBreak => null!;

            public async IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                Interlocked.Increment(ref Passes);
                for (var n = fromBlock; n <= (ulong)Interlocked.Read(ref Head); n++)
                    yield return new BlockBundle(
                        new BlockHeader { BlockNumber = (long)n },
                        new List<ISignedTransaction>(), new List<BlockHeader>(), null, new byte[32]);
                await Task.CompletedTask;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class NoHandshake : IPeerHandshakeWorker
        {
            public Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => throw new NotSupportedException();
        }

        [Fact]
        public async Task S2_01_Given_APassYieldsNothingAtTheTip_When_TheFollowingSourceWaitsForTheNextBlock_Then_PassesAreSpacedByThePollAndTheNextBlockResumesIt()
        {
            var pull = new PassSource { Head = 4 };
            await using var pool = new PeerPoolManager(new NoHandshake(), new PeerPoolOptions(TargetPeerCount: 1, MaxConcurrentDials: 1));
            var source = new FollowingBlockSource(
                pull, pool, _ => Task.FromResult<byte[]>(null!), fallbackPoll: TimeSpan.FromMilliseconds(250));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var seen = new List<ulong>();

            var stream = Task.Run(async () =>
            {
                await foreach (var bundle in source.StreamAsync(5, cts.Token))
                {
                    seen.Add((ulong)bundle.Header.BlockNumber);
                    cts.Cancel();
                }
            });

            await Task.Delay(1000);
            var idlePasses = Volatile.Read(ref pull.Passes);
            Interlocked.Exchange(ref pull.Head, 5);
            await stream;

            Assert.InRange(idlePasses, 1, 6);
            Assert.Equal(new ulong[] { 5 }, seen);
        }

        [Fact]
        public async Task S2_04_Given_AMainnetSizedBlockAtTheMostReceiptsItsGasAllows_When_TheyAreAssembledInPieces_Then_ItIsAccepted()
        {
            const ulong gasUsed = 60_000_000;
            var maxReceipts = (int)(gasUsed / 21_000);
            var assembled = await SyncPeerSession.AssembleReceipts70Async(
                1,
                (pos, firstIndex) => Task.FromResult(((IReadOnlyList<List<Receipt>>)new List<List<Receipt>>
                {
                    Enumerable.Range(0, firstIndex == 0 ? maxReceipts : 1).Select(_ => new Receipt()).ToList()
                }, firstIndex == 0)),
                maxRounds: 8,
                new[] { gasUsed });

            Assert.Single(assembled);
            Assert.Equal(maxReceipts + 1, assembled[0].Count);
        }

        [Fact]
        public async Task S2_04_Given_APeerKeepsExtendingABlockPastWhatItsGasCanHold_When_TheyAreAssembled_Then_AssemblyAbortsWithAMisbehaviourFault()
        {
            const ulong gasUsed = 21_000_000;
            var served = 0;

            await Assert.ThrowsAsync<PeerMisbehaviourException>(() => SyncPeerSession.AssembleReceipts70Async(
                1,
                (pos, firstIndex) =>
                {
                    served++;
                    return Task.FromResult(((IReadOnlyList<List<Receipt>>)new List<List<Receipt>>
                    {
                        Enumerable.Range(0, 1000).Select(_ => new Receipt()).ToList()
                    }, true));
                },
                maxRounds: 4096,
                new[] { gasUsed }));

            Assert.InRange(served, 1, (int)(gasUsed / 4500 / 1000) + 2);
        }

        [Fact]
        public async Task S2_04_Given_APartialBlockWhoseLogsAreLargerThanItsGasCanPayFor_When_TheyAreAssembled_Then_AssemblyAbortsWithAMisbehaviourFault()
        {
            const ulong gasUsed = 100_000;
            var fat = new Receipt
            {
                Logs = new List<Log> { Log.Create(new byte[gasUsed / 8 + 1], "0x" + new string('1', 40)) }
            };

            await Assert.ThrowsAsync<PeerMisbehaviourException>(() => SyncPeerSession.AssembleReceipts70Async(
                1,
                (pos, firstIndex) => Task.FromResult(((IReadOnlyList<List<Receipt>>)new List<List<Receipt>>
                {
                    new List<Receipt> { fat }
                }, true)),
                maxRounds: 4,
                new[] { gasUsed }));
        }

        [Fact]
        public async Task S2_04_Given_AReceiptFetchBreaksTheBound_When_ReceiptsAreFetchedThroughTheScheduler_Then_ThePeerIsQuarantinedAndTheNextPeerServes()
        {
            var liar = new S2Peer(Cursor + 50);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var worker = new S2Worker
            {
                Receipts = (peer, hashes, ct) => peer.Id == liar.Id
                    ? Task.FromException<List<List<Receipt>>>(new PeerMisbehaviourException("bound"))
                    : Task.FromResult(hashes.Select(_ => new List<Receipt>()).ToList())
            };
            var scheduler = SchedulerOf(pool, worker);

            var receipts = await scheduler.FetchReceiptsAsync(new List<byte[]> { new byte[32] }, CancellationToken.None);

            Assert.Single(receipts);
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }
    }
}
