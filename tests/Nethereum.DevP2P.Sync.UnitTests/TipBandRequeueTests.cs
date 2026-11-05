using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
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
    public class TipBandRequeueTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcdc1b6e8b7a0e8b3e1b6c8b0e8c7a0e8b3e1b6c8b0e8c7a0e8b3e1".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        private static readonly HashSet<long> TxBlocks = new() { 3, 4, 5, 6, 7, 8, 9 };

        private static async Task<InMemoryChainStoreBundle> OpenStoreAsync(Chain chain, long executionHead, ulong skeletonHead)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < chain.Headers.Length; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);
            bundle.Metadata.Commit((ulong)executionHead, chain.Hashes[executionHead]);
            SaveSkeletonHead(bundle, skeletonHead);
            return bundle;
        }

        private static void SaveSkeletonHead(IChainStoreBundle bundle, ulong head)
            => bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
            {
                SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = head, Tail = 0, Next = 0 } },
            });

        private static TipBandBodyFollowService NewService(IChainStoreBundle bundle, OnePeerPool pool, TimeSpan? poll = null)
            => new TipBandBodyFollowService(
                new BodyFetchForbiddenScheduler(), pool, bundle, new FixedChainActivations(HardforkName.London),
                logger: null, pollInterval: poll);

        [Fact]
        public async Task Given_ABodyMissingBelowTheCursorHighWater_When_TheBandIsRequeuedFromIt_Then_TheTipBandRefetchesItAndTheSchedulerFetchesNothing()
        {
            var chain = BuildChain(10, TxBlocks);
            using var bundle = await OpenStoreAsync(chain, executionHead: 2, skeletonHead: 9);
            var pool = new OnePeerPool();
            var worker = new ServingWorker(chain);
            var backfiller = new ParallelBlockBackfiller(new BodyFetchForbiddenScheduler(), pool, worker, bundle, role: "tipband");
            var cursor = new ExecutionHeadBodyFillCursor(bundle);
            await cursor.AdvanceAsync(9, CancellationToken.None);
            var service = NewService(bundle, pool);

            Assert.Empty(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[5]));

            service.RequeueFrom(5);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await service.FillOnceAsync(backfiller, cursor, cts.Token);

            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[5]));
            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[9]));
            Assert.Equal(9UL, cursor.Get());
            Assert.Contains(chain.Hashes[5].ToHex(), worker.BodyRequestedHashes);
        }

        [Fact]
        public async Task Given_ABodyHoleAndASlowPollInterval_When_RequeueAndWaitIsCalled_Then_TheRunningBandIsWokenAndFillsTheHole()
        {
            var chain = BuildChain(10, TxBlocks);
            using var bundle = await OpenStoreAsync(chain, executionHead: 2, skeletonHead: 7);
            var pool = new OnePeerPool();
            var worker = new ServingWorker(chain);
            var service = new TipBandBodyFollowService(
                new BodyFetchForbiddenScheduler(), pool, bundle, new FixedChainActivations(HardforkName.London),
                worker, logger: null, pollInterval: TimeSpan.FromHours(1));

            using var runCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var run = service.RunAsync(runCts.Token);

            await WaitUntilAsync(async () => (await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[7])).Count == 1);
            Assert.Empty(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[8]));

            SaveSkeletonHead(bundle, 9);
            using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var filled = await service.RequeueAndWaitAsync(8, waitCts.Token);

            Assert.True(filled);
            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[8]));
            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[9]));

            runCts.Cancel();
            try { await run; } catch (OperationCanceledException) { }
        }

        [Fact]
        public async Task Given_AReorgOfTwoBlocksBelowTheHighWater_When_TheBandIsRequeuedFromTheFork_Then_TheTipBandFetchesTheNewCanonicalBodies()
        {
            var original = BuildChain(10, TxBlocks);
            var forked = BuildChain(10, TxBlocks, forkFrom: 8, forkSalt: 100);
            using var bundle = await OpenStoreAsync(original, executionHead: 2, skeletonHead: 9);
            var pool = new OnePeerPool();
            var worker = new ServingWorker(original, forked);
            var backfiller = new ParallelBlockBackfiller(new BodyFetchForbiddenScheduler(), pool, worker, bundle, role: "tipband");
            var cursor = new ExecutionHeadBodyFillCursor(bundle);
            var service = NewService(bundle, pool);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await service.FillOnceAsync(backfiller, cursor, cts.Token);
            Assert.Equal(9UL, cursor.Get());

            Assert.NotEqual(original.Hashes[8].ToHex(), forked.Hashes[8].ToHex());
            for (int n = 8; n < 10; n++)
                await bundle.Blocks.SaveAsync(forked.Headers[n], forked.Hashes[n]);

            service.RequeueFrom(8);
            await service.FillOnceAsync(backfiller, cursor, cts.Token);

            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(forked.Hashes[8]));
            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(forked.Hashes[9]));
        }

        [Fact]
        public async Task Given_AnUnmatchedBodyForBlockN_When_TheBandFills_Then_FilledToStaysBelowNAndNIsFetchedOnTheNextPass()
        {
            var chain = BuildChain(10, TxBlocks);
            using var bundle = await OpenStoreAsync(chain, executionHead: 2, skeletonHead: 9);
            var pool = new OnePeerPool();
            var worker = new ServingWorker(chain) { WithholdBodyOf = chain.Hashes[5].ToHex() };
            var backfiller = new ParallelBlockBackfiller(new BodyFetchForbiddenScheduler(), pool, worker, bundle, role: "tipband");
            var cursor = new ExecutionHeadBodyFillCursor(bundle);
            var service = NewService(bundle, pool);

            using (var shortCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try { await service.FillOnceAsync(backfiller, cursor, shortCts.Token); }
                catch (OperationCanceledException) { }
            }

            Assert.Equal(4UL, cursor.Get());
            Assert.Empty(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[5]));
            Assert.Contains(chain.Hashes[5].ToHex(), worker.BodyRequestedHashes);

            worker.WithholdBodyOf = null;
            var secondPass = new ParallelBlockBackfiller(new BodyFetchForbiddenScheduler(), pool, worker, bundle, role: "tipband");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await service.FillOnceAsync(secondPass, cursor, cts.Token);

            Assert.Equal(9UL, cursor.Get());
            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[5]));
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline && !await condition())
                await Task.Delay(20);
            Assert.True(await condition());
        }

        private sealed class Chain
        {
            public BlockHeader[] Headers = Array.Empty<BlockHeader>();
            public byte[][] Hashes = Array.Empty<byte[]>();
            public Dictionary<long, BlockBody> Bodies = new();
            public Dictionary<string, long> NumberByHash = new();
        }

        private static Chain BuildChain(int blockCount, HashSet<long> txBlocks, long forkFrom = long.MaxValue, byte forkSalt = 0)
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
                    txs.Add(MakeTx((byte)(n >= forkFrom ? n + forkSalt : n)));
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
            private readonly Chain[] _chains;
            private readonly List<string> _requested = new();
            public ServingWorker(params Chain[] chains) => _chains = chains;
            public string? WithholdBodyOf { get; set; }

            public IReadOnlyList<string> BodyRequestedHashes { get { lock (_requested) return _requested.ToArray(); } }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                {
                    var key = h.ToHex();
                    lock (_requested) _requested.Add(key);
                    if (key == WithholdBodyOf) continue;
                    foreach (var chain in _chains)
                    {
                        if (!chain.NumberByHash.TryGetValue(key, out var n)) continue;
                        bodies.Add(chain.Bodies[n]);
                        break;
                    }
                }
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var rcpts = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                {
                    var found = new List<Receipt>();
                    foreach (var chain in _chains)
                    {
                        if (!chain.NumberByHash.TryGetValue(h.ToHex(), out var n)) continue;
                        if (chain.Bodies[n].Transactions.Count > 0)
                            found.Add(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) });
                        break;
                    }
                    rcpts.Add(found);
                }
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

        private sealed class BodyFetchForbiddenScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new InvalidOperationException("a body was fetched through the scheduler; only the tip band may fetch bodies");
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new InvalidOperationException("a body was fetched through the scheduler; only the tip band may fetch bodies");
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
