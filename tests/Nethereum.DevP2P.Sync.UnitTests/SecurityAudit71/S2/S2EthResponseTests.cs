using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    public class S2EthResponseTests
    {
        private const long Cursor = 100;
        private static readonly byte[] Parent = Enumerable.Repeat((byte)0x11, 32).ToArray();

        private static async Task<BlockBundle?> FirstBundleAsync(DevP2PBlockSource source, TimeSpan budget)
        {
            using var cts = new CancellationTokenSource(budget);
            try
            {
                await foreach (var bundle in source.StreamAsync((ulong)Cursor, cts.Token))
                    return bundle;
            }
            catch (OperationCanceledException) { }
            return null;
        }

        [Fact]
        public async Task S2_01_Given_AFastPeerAnswersEveryBodyRequestEmptyForValidHeaders_When_TheFollowerStreamsTheTip_Then_AnHonestPeerStillServesTheBody()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = async (peer, start, limit, reverse, ct) =>
                {
                    if (peer.Id == honest.Id) await Task.Delay(20, ct);
                    return new List<BlockHeader> { header };
                },
                Bodies = async (peer, hashes, ct) =>
                {
                    if (peer.Id == liar.Id) return new List<BlockBody>();
                    await Task.Delay(20, ct);
                    return hashes.Select(_ => S2Chain.EmptyBody()).ToList();
                }
            };
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            var bundle = await FirstBundleAsync(source, TimeSpan.FromSeconds(8));

            Assert.True(
                bundle != null,
                $"no block in 8s: the empty-answering peer was asked for bodies {worker.CallsOf(liar.Id, "bodies")} times, the honest peer {worker.CallsOf(honest.Id, "bodies")} times");
        }

        [Fact]
        public async Task S2_01_Given_AFastPeerAnswersEveryBodyRequestEmptyForValidHeaders_When_TheFollowerStreamsTheTip_Then_TheEmptyAnsweringPeerIsNotAskedForBodiesWithoutLimit()
        {
            var liar = new S2Peer(ulong.MaxValue / 2);
            var honest = new S2Peer(Cursor + 50);
            var pool = new S2Pool(liar, honest);
            var header = S2Chain.HeaderAt(Cursor, Parent);
            var worker = new S2Worker
            {
                Headers = (peer, start, limit, reverse, ct) => Task.FromResult(new List<BlockHeader> { header }),
                Bodies = (peer, hashes, ct) => Task.FromResult(peer.Id == liar.Id
                    ? new List<BlockBody>()
                    : hashes.Select(_ => S2Chain.EmptyBody()).ToList())
            };
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            var source = new DevP2PBlockSource(pool, scheduler, _ => Task.FromResult<byte[]?>(Parent));

            await FirstBundleAsync(source, TimeSpan.FromSeconds(3));

            Assert.True(
                worker.CallsOf(liar.Id, "bodies") <= 5,
                $"the peer that answered {worker.CallsOf(liar.Id, "bodies")} body requests empty is still being asked");
        }
    }
}
