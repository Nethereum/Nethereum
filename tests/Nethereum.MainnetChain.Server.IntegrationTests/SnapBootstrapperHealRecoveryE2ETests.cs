using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class SnapBootstrapperHealRecoveryE2ETests : IDisposable
    {
        private readonly string _dbPath;

        public SnapBootstrapperHealRecoveryE2ETests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_heal_recovery_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task Heal_SurvivesManyZeroProgressCycles_ThenConvergesOnceAPeerServes()
        {
            var scratch = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(scratch);
            var accountHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xAA });
            var account = new Account
            {
                Nonce = 0,
                Balance = 0UL,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            trie.Put(accountHash, new AccountEncoder().Encode(account));
            trie.SaveNodesToStorage();
            var rootHash = trie.Root.GetHash();
            var leafBlob = scratch.Get(rootHash);

            using var bundle = RocksDbChainStoreBundle.Open(_dbPath);

            var pivotHeader = new BlockHeader
            {
                BlockNumber = 1,
                StateRoot = rootHash,
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            var pivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xFE, 0xED });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = pivotHash,
                HealTargetRoot = rootHash,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });

            var scheduler = new HostileThenServingScheduler(leafBlob, serveAfterCalls: 400);

            var options = new SnapRunOptions
            {
                Scheduler = scheduler,
                PivotRefresher = (_, _) => Task.FromResult<(BlockHeader Header, byte[] Hash)?>(null),
                RunBackfill = false,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance, options, cts.Token);

            Assert.True(result.Ran);
            Assert.True(scheduler.TotalCalls > 320,
                $"expected well over 10 hostile cycles (~32 rounds each) before recovery; TotalCalls={scheduler.TotalCalls}");
            Assert.Equal(1UL, bundle.Metadata.GetLastBlock());
        }

        private sealed class NeverCalledPeer : ISnapPeer
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("heal-only resume must not touch the snap peer");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
        }

        private sealed class HostileThenServingScheduler : IFetchRequestScheduler
        {
            private readonly byte[] _leafBlob;
            private readonly int _serveAfterCalls;
            private int _totalCalls;
            public int TotalCalls => _totalCalls;

            public HostileThenServingScheduler(byte[] leafBlob, int serveAfterCalls)
            {
                _leafBlob = leafBlob;
                _serveAfterCalls = serveAfterCalls;
            }

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                var call = Interlocked.Increment(ref _totalCalls);
                var msg = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                if (call > _serveAfterCalls)
                {
                    msg.Nodes.Add(_leafBlob);
                    for (int i = 1; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                }
                else
                {
                    for (int i = 0; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                }
                return Task.FromResult(msg);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        [Fact]
        public async Task Heal_BogusTinyConvergence_StillThrows_NotSwallowedByRecycleFix()
        {
            var bogusRootNode = new BranchNode { Value = new byte[32] };
            var bogusRootBlob = bogusRootNode.GetEncodedData();
            var bogusRoot = bogusRootNode.GetHash();

            using var bundle = RocksDbChainStoreBundle.Open(_dbPath);

            var pivotHeader = new BlockHeader
            {
                BlockNumber = 1,
                StateRoot = bogusRoot,
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            var pivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xB0, 0x60 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

            var peer = new BogusRootAccountRangePeer(bogusRootBlob);

            var scheduler = new SingleRootNodeScheduler(bogusRootBlob);

            var options = new SnapRunOptions { Scheduler = scheduler, RunBackfill = false };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SnapBootstrapper.RunAsync(bundle, peer, pivotHeader, pivotHash, NullLogger.Instance, options, cts.Token));

            Assert.Contains("bogus-converge guard", ex.Message);
        }

        private sealed class BogusRootAccountRangePeer : ISnapPeer
        {
            private readonly byte[] _proofNode;
            public BogusRootAccountRangePeer(byte[] proofNode) { _proofNode = proofNode; }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
                => Task.FromResult(new AccountRangeMessage
                {
                    RequestId = req.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>(),
                    Proof = new List<byte[]> { _proofNode },
                });

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("zero-account walk must never fetch storage ranges");
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("zero-account walk must never fetch bytecode");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("phase-2 account walk must never fetch trie nodes directly");
        }

        private sealed class SingleRootNodeScheduler : IFetchRequestScheduler
        {
            private readonly byte[] _blob;
            public SingleRootNodeScheduler(byte[] blob) { _blob = blob; }

            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                var msg = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                for (int i = 0; i < paths.Count; i++) msg.Nodes.Add(_blob);
                return Task.FromResult(msg);
            }
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
