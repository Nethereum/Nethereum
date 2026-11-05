using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class ReceiptBackfillRoleTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_StoredBodiesWithoutReceipts_When_ReceiptBackfillRuns_Then_TheBackfillerReceiptRoleFillsThemAndTheStatsMatch()
        {
            const int blocks = 10;
            var keccak = new Sha3Keccack();
            var roots = PatriciaBlockRootsProvider.Instance;
            using var bundle = InMemoryChainStoreBundle.Open();
            var hashes = new byte[blocks][];
            var txsByHash = new Dictionary<string, List<ISignedTransaction>>();
            byte[] prev = new byte[32];
            for (int n = 0; n < blocks; n++)
            {
                var txs = new List<ISignedTransaction>();
                var rcpts = new List<Receipt>();
                if (n >= 3)
                {
                    txs.Add(MakeTx((byte)n));
                    rcpts.Add(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) });
                }
                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prev.Clone(),
                    TransactionsHash = txs.Count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateTransactionsRoot(txs),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = rcpts.Count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateReceiptsRoot(rcpts),
                    StateRoot = new byte[32],
                    Difficulty = new EvmUInt256(1UL),
                    GasLimit = 1,
                    Timestamp = 1,
                    ExtraData = Array.Empty<byte>(),
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    LogsBloom = new byte[256],
                    Coinbase = "0x0000000000000000000000000000000000000000",
                };
                hashes[n] = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                await bundle.Blocks.SaveAsync(header, hashes[n]);
                if (txs.Count > 0) await bundle.Transactions.SaveManyAsync(hashes[n], n, txs);
                txsByHash[hashes[n].ToHex()] = txs;
                prev = hashes[n];
            }
            bundle.Metadata.Commit(9, hashes[9]);
            bundle.Metadata.SetLastFetchedBody(9);

            var worker = new ReceiptWorker(txsByHash);
            var service = new ReceiptBackfillService(
                bundle, new NoFetchScheduler(), new OnePeerPool(), roots,
                new ReceiptBackfillService.Options { IdlePoll = TimeSpan.FromMilliseconds(20), BatchSize = 4 },
                NullLogger.Instance, worker);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var run = service.RunAsync(cts.Token);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(25);
            while (service.Counters.Cursor < 9 && DateTime.UtcNow < deadline) await Task.Delay(20);
            cts.Cancel();
            try { await run; } catch (OperationCanceledException) { }

            Assert.Equal(9UL, service.Counters.Cursor);
            Assert.Equal(9UL, bundle.Metadata.GetReceiptBackfillCursor());
            Assert.Equal(7, service.Counters.ReceiptsPersisted);
            Assert.True(service.Counters.BlocksValidated >= 7);
            Assert.True(service.Counters.BatchesProcessed >= 3);
            for (int n = 3; n < blocks; n++)
                Assert.Single(await bundle.Receipts.GetByBlockHashAsync(hashes[n]));
            Assert.True(worker.ReceiptRequests > 0);
        }

        private static ISignedTransaction MakeTx(byte seed)
        {
            var receiver = new byte[20];
            receiver[0] = seed;
            return new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: new byte[0]);
        }

        private sealed class OnePeerPool : IPeerPool
        {
            private readonly IEthPeer _peer = new FakeEthPeer();
            public IReadOnlyCollection<IEthPeer> ActivePeers => new[] { _peer };
            public int TargetPeerCount => 1;
            public bool IsPeerActive(Guid id) => id == _peer.Id;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class FakeEthPeer : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://peer@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public int EthVersion => 69;
            public ulong PeerLatestBlock => 9;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class ReceiptWorker : IPeerRequestWorker
        {
            private readonly Dictionary<string, List<ISignedTransaction>> _txs;
            private int _receiptRequests;
            public ReceiptWorker(Dictionary<string, List<ISignedTransaction>> txs) => _txs = txs;
            public int ReceiptRequests => Volatile.Read(ref _receiptRequests);

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                Interlocked.Increment(ref _receiptRequests);
                var result = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                {
                    var list = new List<Receipt>();
                    if (_txs.TryGetValue(h.ToHex(), out var txs))
                        foreach (var _ in txs) list.Add(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) });
                    result.Add(list);
                }
                return Task.FromResult(result);
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new InvalidOperationException("the receipt role must not fetch bodies");
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class NoFetchScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new InvalidOperationException("receipts must be fetched by the backfiller receipt role, not the scheduler");
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
