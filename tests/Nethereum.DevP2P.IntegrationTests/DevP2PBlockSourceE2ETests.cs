using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class DevP2PBlockSourceE2ETests
    {
        private static readonly Sha3Keccack Keccak = new();

        [Fact]
        public async Task Stream_ReturnsBundlesInOrder()
        {
            var headers = MakeChainedHeaders(startBlock: 100, count: 5);
            var bodies = headers.Select(_ => new BlockBody { Transactions = new List<ISignedTransaction>() }).ToList();

            var scheduler = new StubScheduler(headers, bodies);
            var parentLookup = (ulong block) =>
                Task.FromResult<byte[]?>(headers[0].ParentHash);

            await using var source = new DevP2PBlockSource(
                new EmptyPool(), scheduler, parentLookup,
                headerBatchSize: 5, bodyBatchSize: 5);

            var collected = new List<ulong>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await foreach (var bundle in source.StreamAsync(100, cts.Token))
            {
                collected.Add((ulong)bundle.Header.BlockNumber);
                if (collected.Count >= 5) break;
            }

            Assert.Equal(new ulong[] { 100, 101, 102, 103, 104 }, collected);
            Assert.Null(source.LastChainBreak);
            return;
        }

        [Fact]
        public async Task Stream_ChainBreak_FirstHeaderParentMismatch_CompletesViaLastChainBreak()
        {
            var headers = MakeChainedHeaders(startBlock: 100, count: 3);
            var bodies = headers.Select(_ => new BlockBody { Transactions = new List<ISignedTransaction>() }).ToList();

            var ourParent = new byte[32];
            for (int i = 0; i < 32; i++) ourParent[i] = 0xCC;

            Assert.False(ByteUtil.AreEqual(ourParent, headers[0].ParentHash));

            var scheduler = new StubScheduler(headers, bodies);
            var parentLookup = (ulong block) => Task.FromResult<byte[]?>(ourParent);

            await using var source = new DevP2PBlockSource(
                new EmptyPool(), scheduler, parentLookup,
                headerBatchSize: 3, bodyBatchSize: 3);

            var collected = new List<ulong>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await foreach (var bundle in source.StreamAsync(100, cts.Token))
            {
                collected.Add((ulong)bundle.Header.BlockNumber);
            }

            Assert.Empty(collected);
            Assert.NotNull(source.LastChainBreak);
            Assert.Equal(100UL, source.LastChainBreak!.AtBlock);
            Assert.Equal(ourParent, source.LastChainBreak.OurParentHash);
            Assert.Equal(headers[0].ParentHash, source.LastChainBreak.PeerParentHash);
        }

        [Fact]
        public async Task Stream_IntraBatchParentBreak_TruncatesBatch()
        {
            var headers = MakeChainedHeaders(startBlock: 100, count: 4);
            var corruptParent = new byte[32];
            for (int i = 0; i < 32; i++) corruptParent[i] = 0xFF;
            headers[2].ParentHash = corruptParent;

            var bodies = headers.Select(_ => new BlockBody { Transactions = new List<ISignedTransaction>() }).ToList();

            var scheduler = new StubScheduler(headers, bodies);
            var parentLookup = (ulong block) => Task.FromResult<byte[]?>(headers[0].ParentHash);

            await using var source = new DevP2PBlockSource(
                new EmptyPool(), scheduler, parentLookup,
                headerBatchSize: 4, bodyBatchSize: 4);

            var collected = new List<ulong>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await foreach (var bundle in source.StreamAsync(100, cts.Token))
            {
                collected.Add((ulong)bundle.Header.BlockNumber);
                if (collected.Count >= 2) break;
            }

            Assert.Equal(new ulong[] { 100, 101 }, collected);
            Assert.Null(source.LastChainBreak);
            return;
        }

        private static readonly byte[] EmptyTxRoot = Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH;
        private static readonly byte[] EmptyUnclesHash = Keccak.CalculateHash(RLP.RLP.EncodeList());

        [Fact]
        public async Task Stream_BodyTxRootMismatch_DropsBundleAndDoesNotAdvanceCursor()
        {
            // Header at block 200 claims an empty body (TransactionsHash = EMPTY_TRIE_HASH),
            // but the peer returns a body containing real transactions whose tx root
            // does not match the header. This is the exact shape of the mainnet
            // block 742497 misalignment that produced a state-root divergence under sync.
            var headers = MakeChainedHeaders(startBlock: 200, count: 2);

            // Fake legacy tx so we actually have a body to mis-pair.
            var ghostTxRlp = ("0xf87083011b45850ba43b740083015f9094c2b0fb302729ef0f4aa96c0e02cc2346459294df"
                              + "881d107520b1393400801ba0deb60d02ae3cff6963377b90c7187ffa1b9863e6d6f2b1eab996b8cf3483c5b2"
                              + "a019782fe68d72ea1abce7176db37e39516d7f9db73941e819ded43174a836a501")
                .HexToByteArray();
            var ghostTx = TransactionFactory.CreateTransaction(ghostTxRlp);

            var goodBody = new BlockBody { Transactions = new List<ISignedTransaction>() };
            var badBody = new BlockBody { Transactions = new List<ISignedTransaction> { ghostTx } };

            var scheduler = new StubScheduler(headers, new List<BlockBody> { badBody, goodBody });
            var parentLookup = (ulong block) => Task.FromResult<byte[]?>(headers[0].ParentHash);

            await using var source = new DevP2PBlockSource(
                new EmptyPool(), scheduler, parentLookup,
                headerBatchSize: 2, bodyBatchSize: 2);

            var collected = new List<ulong>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try
            {
                await foreach (var bundle in source.StreamAsync(200, cts.Token))
                {
                    collected.Add((ulong)bundle.Header.BlockNumber);
                }
            }
            catch (OperationCanceledException) { /* expected once scheduler exhausts */ }

            Assert.Empty(collected);
            Assert.Null(source.LastChainBreak);
        }

        private static List<BlockHeader> MakeChainedHeaders(ulong startBlock, int count)
        {
            var headers = new List<BlockHeader>(count);
            var prevHash = new byte[32];
            for (int i = 0; i < 32; i++) prevHash[i] = 0xAA;

            for (int i = 0; i < count; i++)
            {
                var header = new BlockHeader
                {
                    BlockNumber = startBlock + (ulong)i,
                    ParentHash = prevHash,
                    UnclesHash = EmptyUnclesHash,
                    Coinbase = "0x" + new string('0', 40),
                    StateRoot = new byte[32],
                    TransactionsHash = EmptyTxRoot,
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    Difficulty = 0,
                    GasLimit = 0,
                    GasUsed = 0,
                    Timestamp = 0,
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8]
                };
                headers.Add(header);

                var encoded = new BlockHeaderEncoder().Encode(header);
                prevHash = Keccak.CalculateHash(encoded);
            }
            return headers;
        }

        private sealed class StubScheduler : IFetchRequestScheduler
        {
            private readonly List<BlockHeader> _headers;
            private readonly List<BlockBody> _bodies;
            private int _headerCallCount;

            public StubScheduler(List<BlockHeader> headers, List<BlockBody> bodies)
            {
                _headers = headers;
                _bodies = bodies;
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct)
            {
                if (Interlocked.Increment(ref _headerCallCount) > 1)
                    return Task.FromResult(new List<BlockHeader>());

                var first = _headers.FindIndex(h => (ulong)h.BlockNumber == startBlock);
                if (first < 0) return Task.FromResult(new List<BlockHeader>());
                var take = Math.Min((int)limit, _headers.Count - first);
                return Task.FromResult(_headers.GetRange(first, take));
            }

            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                var result = new List<BlockBody>();
                foreach (var hash in blockHashes)
                {
                    var idx = _headers.FindIndex(h =>
                    {
                        var encoded = new BlockHeaderEncoder().Encode(h);
                        return ByteUtil.AreEqual(Keccak.CalculateHash(encoded), hash);
                    });
                    if (idx >= 0) result.Add(_bodies[idx]);
                }
                return Task.FromResult(result);
            }
        }

        private sealed class EmptyPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }
    }
}
