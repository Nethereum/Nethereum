using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class S1aR9AnnouncedHeadReviewTests
    {
        private const ulong Cursor = 100;

        private sealed class AnnouncingPeer : IEthPeer
        {
            private static int _next;

            public AnnouncingPeer(ulong announcedHead)
            {
                PeerLatestBlock = announcedHead;
                Enode = $"enode://{new string('b', 128)}@127.0.0.1:{31000 + Interlocked.Increment(ref _next)}";
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host => "127.0.0.1";
            public int EthVersion => 71;
            public ulong PeerLatestBlock { get; }
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class FixedPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public FixedPool(params IEthPeer[] peers) { _peers = peers.ToList(); }
            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct)
            {
                _peers.RemoveAll(p => p.Id == peerId);
                return Task.CompletedTask;
            }
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class HeaderWorker : IPeerRequestWorker
        {
            private readonly Guid _liar;
            private readonly int _honestDelayMs;
            public int LiarCalls;
            public int HonestCalls;

            public HeaderWorker(Guid liar, int honestDelayMs) { _liar = liar; _honestDelayMs = honestDelayMs; }

            public async Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                if (peer.Id == _liar)
                {
                    Interlocked.Increment(ref LiarCalls);
                    return new List<BlockHeader>();
                }
                Interlocked.Increment(ref HonestCalls);
                await Task.Delay(_honestDelayMs, ct);
                return new List<BlockHeader> { new BlockHeader { BlockNumber = (long)startBlock } };
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private static async Task<int> HeaderFetchesThatServedAsync(ulong honestAnnouncedHead, int attempts)
        {
            var liar = new AnnouncingPeer(ulong.MaxValue / 2);
            var honest = new AnnouncingPeer(honestAnnouncedHead);
            var pool = new FixedPool(liar, honest);
            var worker = new HeaderWorker(liar.Id, honestDelayMs: 30);
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var served = 0;
            for (var i = 0; i < attempts; i++)
            {
                var headers = await scheduler.FetchAnnouncedHeadersAsync(Cursor, 64, cts.Token);
                if (headers.Count > 0) served++;
            }
            return served;
        }

        [Fact]
        public async Task S1aR9_01_Given_APeerAnnouncesAHeadFarAboveTheTruthAndAnswersEveryHeaderRequestEmpty_When_TheTipIsFetchedRepeatedly_Then_AnHonestPeerWhoseAnnouncedHeadIsStaleStillServes()
        {
            var served = await HeaderFetchesThatServedAsync(honestAnnouncedHead: Cursor - 5, attempts: 10);

            Assert.True(served > 0, "10 announced-header fetches: the liar answered all of them empty and the honest peer was never asked");
        }

        [Fact]
        public async Task S1aR9_01_Given_APeerAnnouncesAHeadFarAboveTheTruthAndAnswersFasterThanTheHonestPeer_When_TheTipIsFetchedRepeatedly_Then_TheHonestPeerStillServes()
        {
            var served = await HeaderFetchesThatServedAsync(honestAnnouncedHead: Cursor, attempts: 10);

            Assert.True(served > 0, $"10 announced-header fetches: {served} served; the faster liar wins every claim");
        }
    }
}
