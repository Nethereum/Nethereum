using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TipBandBodyFollowServiceTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcdc1b6e8b7a0e8b3e1b6c8b0e8c7a0e8b3e1b6c8b0e8c7a0e8b3e1".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-sync", "Tip-range job fills [executionHead+1..tip] independently of the history drain")]
        public async Task FillOnce_FillsExecutionHeadToTip_IndependentOfArchiveBodyCursor()
        {
            var chain = BuildChain(blockCount: 10, txBlocks: new HashSet<long> { 3, 4, 5, 6, 7, 8, 9 });
            using var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 10; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);
            bundle.Metadata.Commit(2, chain.Hashes[2]);
            bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
            {
                SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 9, Tail = 0, Next = 0 } },
            });

            var pool = new OnePeerPool();
            var worker = new ServingWorker(chain);
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, worker, bundle);
            var cursor = new ExecutionHeadBodyFillCursor(bundle);
            var service = new TipBandBodyFollowService(
                new UnusedScheduler(), pool, bundle, new FixedChainActivations(HardforkName.London));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await service.FillOnceAsync(backfiller, cursor, cts.Token);

            Assert.Equal(9UL, cursor.Get());

            Assert.Equal(0UL, bundle.Metadata.GetLastFetchedBody());
            Assert.Equal(0UL, bundle.Metadata.GetLastFetchedHeader());

            var block9Txs = await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[9]);
            Assert.Single(block9Txs);
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
            var c = new Chain
            {
                Headers = new BlockHeader[blockCount],
                Hashes = new byte[blockCount][],
            };

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

        private sealed class ServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            public ServingWorker(Chain chain) => _chain = chain;

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                    bodies.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Bodies[n] : new BlockBody());
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
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
