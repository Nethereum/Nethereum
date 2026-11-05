using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    public class S2HeaderReplyTests
    {
        private const long Cursor = 100;
        private static readonly byte[] Parent = Enumerable.Repeat((byte)0x11, 32).ToArray();

        [Fact]
        public async Task S2_02_Given_AClaimantAnswersHeadersFromTheWrongBlockNumberChainedToOurParent_When_TheAnnouncedHeadersAreFetched_Then_TheReplyIsRefusedAndThePeerQuarantined()
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
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchAnnouncedHeadersAsync((ulong)Cursor, 64, CancellationToken.None);

            Assert.Equal((EvmUInt256)Cursor, headers[0].BlockNumber);
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task S2_02_Given_AClaimantAnswersHeadersFromTheWrongBlockNumberChainedToOurParent_When_TheFollowerStreamsTheTip_Then_TheFirstBundleIsTheRequestedBlock()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var wrong = S2Chain.HeaderAt(Cursor + 7, Parent);
            var right = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) => Task.FromResult(
                    new List<BlockHeader> { peer.Id == liar.Id ? wrong : right }),
                Bodies = (peer, hashes, ct) => Task.FromResult(hashes.Select(_ => S2Chain.EmptyBody()).ToList())
            };
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            BlockBundle? first = null;
            await foreach (var bundle in source.StreamAsync((ulong)Cursor, cts.Token))
            {
                first = bundle;
                break;
            }

            Assert.NotNull(first);
            Assert.Equal((EvmUInt256)Cursor, first!.Header.BlockNumber);
        }

        [Fact]
        public async Task S2_03_Given_APeerAnswersAReverseHeaderBatchWithABlockNumberAbove2Pow64_When_TheBackwardWalkRuns_Then_TheBatchIsRefusedWithoutAnExceptionAndThePeerQuarantined()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = new List<BlockHeader>();
            var hashes = new List<byte[]>();
            var prev = new byte[32];
            for (long n = 0; n < 20; n++)
            {
                var header = S2Chain.HeaderAt(n, prev);
                chain.Add(header);
                prev = S2Chain.HashOf(header);
                hashes.Add(prev);
            }

            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(19);
            var pool = new S2Pool(liar, honest);
            var forged = S2Chain.HeaderAt(19, S2Chain.HashOf(chain[18]));
            forged.BlockNumber = (EvmUInt256)(BigInteger.One << 64);
            forged.Coinbase = "0x" + new string('0', 40);
            forged.StateRoot = new byte[32];
            forged.LogsBloom = new byte[256];
            forged.MixHash = new byte[32];
            forged.Nonce = new byte[8];
            var wire = BlockHeadersMessageEncoder.Encode(
                new BlockHeadersMessage { RequestId = 1, Headers = new List<BlockHeader> { forged } });
            var huge = BlockHeadersMessageEncoder.Decode(wire).Headers[0];
            Assert.Equal((EvmUInt256)(BigInteger.One << 64), huge.BlockNumber);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) =>
                {
                    if (peer.Id == liar.Id) return Task.FromResult(new List<BlockHeader> { huge });
                    var batch = new List<BlockHeader>();
                    for (long n = (long)start; n >= 0 && batch.Count < (long)limit; n--) batch.Add(chain[(int)n]);
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

            var fault = await Record.ExceptionAsync(() => walker.WalkAsync(
                19, S2Chain.HashOf(chain[19]), 0,
                (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None));

            Assert.Null(fault);
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task S2_05_Given_APeerTimesOutTwiceThenAnswersEmptyInstantlyInARepeatingCycle_When_HeadersAreFetched_Then_ThePeerIsDroppedForRepeatedTimeouts()
        {
            var attacker = new S2Peer(Cursor + 50);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(attacker, honest);
            var right = S2Chain.HeaderAt(Cursor, Parent);
            var attackerCalls = 0;
            var worker = new S2Worker
            {
                Headers = async (peer, start, limit, reverse, ct) =>
                {
                    if (peer.Id != attacker.Id)
                    {
                        await Task.Delay(1200, ct);
                        return new List<BlockHeader> { right };
                    }
                    var call = Interlocked.Increment(ref attackerCalls);
                    if (call % 3 == 1) return new List<BlockHeader>();
                    await Task.Delay(Timeout.Infinite, ct);
                    return new List<BlockHeader>();
                }
            };
            var scheduler = new FetchRequestScheduler(
                pool, worker, new FetchRequestSchedulerOptions(PerRequestTimeout: TimeSpan.FromSeconds(3), MaxInFlightPerPeer: 1))
            {
                HeaderBatchQuarantineDuration = TimeSpan.Zero
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            for (var i = 0; i < 12 && pool.Drops == 0 && attackerCalls < 12; i++)
                await scheduler.FetchHeadersAsync((ulong)Cursor, 8, cts.Token);

            Assert.True(
                pool.Drops > 0,
                $"the peer made {attackerCalls} requests, 2 of every 3 timed out, and it was never dropped");
        }
    }
}
