using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerHeadersByHashTests
    {
        private static BlockHeader HeaderAt(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = number,
            ParentHash = parentHash,
            Timestamp = 1000 + number
        };

        [Fact]
        public async Task Given_a_peer_that_answers_by_hash_with_a_header_of_another_hash_When_the_headers_are_fetched_Then_the_peer_is_penalised_and_the_honest_peer_answers()
        {
            var genuine = HeaderAt(10, new byte[32]);
            var genuineHash = BlockHashCalculator.ForHeader(genuine);
            var forged = HeaderAt(99, new byte[32]);
            var pool = new Pool(2);
            var liar = pool.ActivePeers.First();
            var worker = new Worker(peer => peer.Id == liar.Id ? new List<BlockHeader> { forged } : new List<BlockHeader> { genuine });
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersByHashAsync(genuineHash, 1, CancellationToken.None);

            Assert.Same(genuine, Assert.Single(headers));
            Assert.True(scheduler.IsHeaderPeerQuarantined(liar.Id));
        }

        [Fact]
        public async Task Given_only_a_peer_that_answers_by_hash_with_a_header_of_another_hash_When_the_headers_are_fetched_Then_the_fetch_fails()
        {
            var genuineHash = BlockHashCalculator.ForHeader(HeaderAt(10, new byte[32]));
            var pool = new Pool(1);
            var worker = new Worker(_ => new List<BlockHeader> { HeaderAt(99, new byte[32]) });
            var scheduler = new FetchRequestScheduler(
                pool, worker, new FetchRequestSchedulerOptions(MaxRetriesPerRequest: 1, MaxInFlightPerPeer: 1));

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersByHashAsync(genuineHash, 1, CancellationToken.None));
        }

        [Fact]
        public async Task Given_a_peer_whose_later_by_hash_headers_do_not_link_to_the_first_When_the_headers_are_fetched_Then_the_peer_is_penalised()
        {
            var first = HeaderAt(10, new byte[32]);
            var firstHash = BlockHashCalculator.ForHeader(first);
            var unlinked = HeaderAt(11, new byte[32]);
            var pool = new Pool(1);
            var peer = pool.ActivePeers.Single();
            var worker = new Worker(_ => new List<BlockHeader> { first, unlinked });
            var scheduler = new FetchRequestScheduler(
                pool, worker, new FetchRequestSchedulerOptions(MaxRetriesPerRequest: 1, MaxInFlightPerPeer: 1));

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersByHashAsync(firstHash, 2, CancellationToken.None));
            Assert.True(scheduler.IsHeaderPeerQuarantined(peer.Id));
        }

        [Fact]
        public async Task Given_a_peer_that_answers_by_hash_with_a_linked_chain_from_the_requested_hash_When_the_headers_are_fetched_Then_the_headers_are_returned()
        {
            var first = HeaderAt(10, new byte[32]);
            var firstHash = BlockHashCalculator.ForHeader(first);
            var second = HeaderAt(11, firstHash);
            var pool = new Pool(1);
            var worker = new Worker(_ => new List<BlockHeader> { first, second });
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersByHashAsync(firstHash, 2, CancellationToken.None);

            Assert.Equal(2, headers.Count);
        }

        private sealed class Worker : IPeerRequestWorker
        {
            private readonly Func<IEthPeer, List<BlockHeader>> _answer;
            public Worker(Func<IEthPeer, List<BlockHeader>> answer) { _answer = answer; }

            public Task<List<BlockHeader>> GetHeadersByHashAsync(IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
                => Task.FromResult(_answer(peer));

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class Pool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;

            public Pool(int count)
            {
                _peers = Enumerable.Range(0, count)
                    .Select(i => (IEthPeer)new Peer($"enode://{new string('a', 128)}@127.0.0.1:{31000 + i}"))
                    .ToList();
            }

            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class Peer : IEthPeer
        {
            public Peer(string enode) { Enode = enode; Host = enode; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
