using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
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
    public class BackfillerDeadPeerCooldownTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcdc1b6e8b7a0e8b3e1b6c8b0e8c7a0e8b3e1b6c8b0e8c7a0e8b3e1".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task DeadPeer_IsCooledDown_NotSpinned_AndHealthyPeerCompletesTheFill()
        {
            var chain = BuildChain(blockCount: 10, txBlocks: new HashSet<long> { 3, 4, 5, 6, 7 });
            using var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 10; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var deadPeer = new FakeEthPeer();
            var healthyPeer = new FakeEthPeer();
            var pool = new TwoPeerPool(deadPeer, healthyPeer);
            var worker = new DeadAndServingWorker(chain, deadPeerId: deadPeer.Id);
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, worker, bundle);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await backfiller.BackfillAsync(3, 7, headersFromStore: true, cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(5UL, result.BlocksWritten);
            Assert.Equal(5UL, result.TransactionsWritten);

            Assert.True(worker.DeadPeerCalls < 50,
                $"dead peer was dispatched {worker.DeadPeerCalls} times — failure cooldown is not gating the fetcher loop");
            Assert.Contains(healthyPeer.Id, pool.ReportSuccessCalls);
        }

        [Fact]
        public async Task PeerVanishesMidReservation_PeerRemovedRevertsItsBlocks_AndAnotherPeerCompletesTheFill()
        {
            var chain = BuildChain(blockCount: 10, txBlocks: new HashSet<long> { 3, 4, 5, 6, 7 });
            using var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 10; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var vanishingPeer = new FakeEthPeer();
            var healthyPeer = new FakeEthPeer();
            var pool = new TwoPeerPool(vanishingPeer, healthyPeer);
            var worker = new HangingThenServingWorker(chain, hangPeerId: vanishingPeer.Id);
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, worker, bundle);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var fillTask = backfiller.BackfillAsync(3, 7, headersFromStore: true, cts.Token);

            await worker.FirstHang.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pool.RaisePeerRemoved(vanishingPeer);

            var result = await fillTask;

            Assert.True(result.Ran);
            Assert.Equal(5UL, result.BlocksWritten);
            Assert.Equal(5UL, result.TransactionsWritten);
        }


        private sealed class Chain
        {
            public BlockHeader[] Headers = Array.Empty<BlockHeader>();
            public byte[][] Hashes = Array.Empty<byte[]>();
            public Dictionary<long, BlockBody> Bodies = new();
            public Dictionary<long, List<Receipt>> Receipts = new();
            public Dictionary<string, long> NumberByHash = new();
        }

        private static Chain BuildChain(int blockCount, HashSet<long> txBlocks)
        {
            var keccak = new Sha3Keccack();
            var roots = PatriciaBlockRootsProvider.Instance;
            var c = new Chain { Headers = new BlockHeader[blockCount], Hashes = new byte[blockCount][] };

            byte[] prevHash = new byte[32];
            for (long n = 0; n < blockCount; n++)
            {
                var txs = new List<ISignedTransaction>();
                var rcpts = new List<Receipt>();
                if (txBlocks.Contains(n))
                {
                    txs.Add(MakeTx((byte)n));
                    rcpts.Add(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) });
                }

                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prevHash.Clone(),
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

                var hash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                c.Headers[n] = header;
                c.Hashes[n] = hash;
                c.Bodies[n] = new BlockBody { Transactions = txs, Uncles = new List<BlockHeader>() };
                c.Receipts[n] = rcpts;
                c.NumberByHash[hash.ToHex()] = n;
                prevHash = hash;
            }
            return c;
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

        private sealed class TwoPeerPool : IPeerPool
        {
            private readonly IEthPeer[] _peers;
            private readonly HashSet<Guid> _removed = new();
            public List<Guid> ReportSuccessCalls { get; } = new();
            public TwoPeerPool(params IEthPeer[] peers) => _peers = peers;
            private bool IsRemoved(Guid id) { lock (_removed) return _removed.Contains(id); }
            public IReadOnlyCollection<IEthPeer> ActivePeers
            {
                get { var list = new List<IEthPeer>(); foreach (var p in _peers) if (!IsRemoved(p.Id)) list.Add(p); return list; }
            }
            public int TargetPeerCount => _peers.Length;
            public bool IsPeerActive(Guid id) { if (IsRemoved(id)) return false; foreach (var p in _peers) if (p.Id == id) return true; return false; }
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public void RaisePeerRemoved(IEthPeer peer)
            {
                lock (_removed) _removed.Add(peer.Id);
                PeerRemoved?.Invoke(this, peer);
            }
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) => ReportSuccessCalls.Add(peerId);
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

        private sealed class DeadAndServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            private readonly Guid _deadPeerId;
            private int _deadPeerCalls;

            public DeadAndServingWorker(Chain chain, Guid deadPeerId)
            {
                _chain = chain;
                _deadPeerId = deadPeerId;
            }

            public int DeadPeerCalls => Volatile.Read(ref _deadPeerCalls);

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                if (peer.Id == _deadPeerId)
                {
                    Interlocked.Increment(ref _deadPeerCalls);
                    throw new IOException("broken pipe");
                }
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                    bodies.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Bodies[n] : new BlockBody());
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                if (peer.Id == _deadPeerId)
                {
                    Interlocked.Increment(ref _deadPeerCalls);
                    throw new IOException("broken pipe");
                }
                var rcpts = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                    rcpts.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Receipts[n] : new List<Receipt>());
                return Task.FromResult(rcpts);
            }

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new NotImplementedException("headers come from the store in this mode");
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private sealed class HangingThenServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            private readonly Guid _hangPeerId;
            public TaskCompletionSource FirstHang { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public HangingThenServingWorker(Chain chain, Guid hangPeerId) { _chain = chain; _hangPeerId = hangPeerId; }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                if (peer.Id == _hangPeerId) return Hang<List<BlockBody>>(ct);
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                    bodies.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Bodies[n] : new BlockBody());
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                if (peer.Id == _hangPeerId) return Hang<List<List<Receipt>>>(ct);
                var rcpts = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                    rcpts.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Receipts[n] : new List<Receipt>());
                return Task.FromResult(rcpts);
            }

            private Task<T> Hang<T>(CancellationToken ct)
            {
                FirstHang.TrySetResult();
                var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            }

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new NotImplementedException("headers come from the store in this mode");
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private sealed class UnusedScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
