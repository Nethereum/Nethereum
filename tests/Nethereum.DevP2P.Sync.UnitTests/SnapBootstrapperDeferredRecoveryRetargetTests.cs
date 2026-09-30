using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperDeferredRecoveryRetargetTests
    {
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();

        [Fact]
        public async Task Given_DebtFetchedAtAnExpiredRootButAccountServableAtFreshPivot_When_FinalRootsAreResolved_Then_ItReTargetsToTheFreshRootWithoutDownloadingStorageRanges()
        {
            var accountHash = Keccak.CalculateHash(new byte[] { 0x0A, 0xAA });
            var (serverB, rootB, storageRootB, _) = BuildServableState(accountHash, storageSeed: 0x77, slotCount: 40);

            var rootA = Hash32(0xA1);

            using var inner = InMemoryChainStoreBundle.Open();
            using var bundle = new StateTrieOverrideBundle(inner, new RawReadablePathNodeStore());
            bundle.Metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = storageRootB,
                FetchStateRoot = rootA,
                FetchPivotBlock = 100UL,
                Reason = DeferredStorageReason.BigAccountChunked,
                Status = StorageCompleteness.DeferredBigAccount,
            });

            var scheduler = new RootGatedScheduler(new PatriciaSnapRequestHandler(serverB, new NullBytecodes()), servableStateRoot: rootB);

            var pivot = new SnapBootstrapper.RollingPivot(Header(100, rootA), Hash32(0xB0));
            var headerB = Header(228, rootB);
            var hashB = Hash32(0xB1);
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((headerB, hashB));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await SnapBootstrapper.ResolveOpenDebtFinalRootsWithRetargetAsync(
                bundle, scheduler, pivot, pivotRefresher, NullLogger.Instance, cts.Token);

            var debt = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(StorageCompleteness.FinalRootReResolved, debt.Status);
            Assert.Equal(rootB.ToHex(), debt.FinalStateRoot.ToHex());
            Assert.Equal(storageRootB.ToHex(), debt.FinalStorageRoot.ToHex());
            Assert.Equal(rootB.ToHex(), pivot.Current.Header.StateRoot.ToHex());
            Assert.True(scheduler.AccountRangeCallsForServableRoot > 0, "the final storage root must have been proven at root B");
            Assert.Equal(0, scheduler.StorageRangeCallsForServableRoot);
        }

        [Fact]
        public async Task Given_DebtServableAtTheCurrentPivot_When_FinalRootsAreResolved_Then_ThePivotIsNotAdvanced()
        {
            var accountHash = Keccak.CalculateHash(new byte[] { 0x0A, 0xAA });
            var (serverB, rootB, storageRootB, _) = BuildServableState(accountHash, storageSeed: 0x78, slotCount: 10);

            using var inner = InMemoryChainStoreBundle.Open();
            using var bundle = new StateTrieOverrideBundle(inner, new RawReadablePathNodeStore());
            bundle.Metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = storageRootB,
                FetchStateRoot = rootB,
                FetchPivotBlock = 228UL,
                Reason = DeferredStorageReason.BigAccountChunked,
                Status = StorageCompleteness.DeferredBigAccount,
            });

            var scheduler = new RootGatedScheduler(new PatriciaSnapRequestHandler(serverB, new NullBytecodes()), servableStateRoot: rootB);
            var pivot = new SnapBootstrapper.RollingPivot(Header(228, rootB), Hash32(0xB1));
            var refresherCalls = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher = (_, _) =>
            {
                refresherCalls++;
                return Task.FromResult<(BlockHeader, byte[])?>(null);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await SnapBootstrapper.ResolveOpenDebtFinalRootsWithRetargetAsync(
                bundle, scheduler, pivot, pivotRefresher, NullLogger.Instance, cts.Token);

            var debt = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(StorageCompleteness.FinalRootReResolved, debt.Status);
            Assert.Equal(rootB.ToHex(), debt.FinalStateRoot.ToHex());
            Assert.Equal(storageRootB.ToHex(), debt.FinalStorageRoot.ToHex());
            Assert.Equal(0, refresherCalls);
            Assert.Equal(0, scheduler.StorageRangeCallsForServableRoot);
        }

        [Fact]
        public async Task Given_ADebtAlreadyResolvedAtAnExpiredRoot_When_TheCurrentPivotCannotProveIt_Then_ThePivotIsAdvancedAndTheDebtIsReResolvedAtTheFreshRoot()
        {
            var accountHash = Keccak.CalculateHash(new byte[] { 0x0A, 0xAA });
            var (serverB, rootB, storageRootB, _) = BuildServableState(accountHash, storageSeed: 0x79, slotCount: 12);

            var expiredRoot = Hash32(0xA0);
            var expiredStorageRoot = Hash32(0x5A);
            var rootA = Hash32(0xA1);

            using var inner = InMemoryChainStoreBundle.Open();
            using var bundle = new StateTrieOverrideBundle(inner, new RawReadablePathNodeStore());
            bundle.Metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = expiredStorageRoot,
                FetchStateRoot = expiredRoot,
                FetchPivotBlock = 50UL,
                FinalStateRoot = expiredRoot,
                FinalStorageRoot = expiredStorageRoot,
                Reason = DeferredStorageReason.BigAccountChunked,
                Status = StorageCompleteness.FinalRootReResolved,
            });

            var scheduler = new RootGatedScheduler(new PatriciaSnapRequestHandler(serverB, new NullBytecodes()), servableStateRoot: rootB);

            var pivot = new SnapBootstrapper.RollingPivot(Header(100, rootA), Hash32(0xB0));
            var headerB = Header(228, rootB);
            var hashB = Hash32(0xB1);
            var refresherCalls = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher = (_, _) =>
            {
                refresherCalls++;
                return Task.FromResult<(BlockHeader, byte[])?>((headerB, hashB));
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await SnapBootstrapper.ResolveOpenDebtFinalRootsWithRetargetAsync(
                bundle, scheduler, pivot, pivotRefresher, NullLogger.Instance, cts.Token);

            var debt = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(1, refresherCalls);
            Assert.Equal(rootB.ToHex(), pivot.Current.Header.StateRoot.ToHex());
            Assert.Equal(StorageCompleteness.FinalRootReResolved, debt.Status);
            Assert.Equal(rootB.ToHex(), debt.FinalStateRoot.ToHex());
            Assert.Equal(storageRootB.ToHex(), debt.FinalStorageRoot.ToHex());
            Assert.Equal(0, scheduler.StorageRangeCallsForServableRoot);
        }

        private static (InMemoryContentNodeStore Server, byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Slots)
            BuildServableState(byte[] accountHash, byte storageSeed, int slotCount)
        {
            var server = new InMemoryContentNodeStore();

            var storageTrie = new PatriciaTrie(server, Hp);
            var slots = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < slotCount; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { (byte)(i >> 8), (byte)(i & 0xFF), storageSeed });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i & 0xFF), (byte)((i >> 4) & 0xFF), 0xEE });
                slots.Add((slotHash, value));
                storageTrie.Put(slotHash, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            byte seed = 0;
            byte[] decoy;
            do { decoy = Keccak.CalculateHash(new byte[] { 0x0D, 0xEC, seed++ }); }
            while (decoy[0] >> 4 == accountHash[0] >> 4);

            var accountTrie = new PatriciaTrie(server, Hp);
            accountTrie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            accountTrie.Put(decoy, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1UL,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            accountTrie.SaveDirtyNodesToStorage();

            slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));
            return (server, accountTrie.Root.GetHash(), storageRoot, slots);
        }

        private static BlockHeader Header(long blockNumber, byte[] stateRoot) => new BlockHeader
        {
            BlockNumber = blockNumber,
            StateRoot = stateRoot,
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

        private static byte[] Hash32(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class RootGatedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly byte[] _servable;
            public int StorageRangeCallsForServableRoot { get; private set; }
            public int AccountRangeCallsForServableRoot { get; private set; }

            public RootGatedScheduler(PatriciaSnapRequestHandler handler, byte[] servableStateRoot)
            {
                _handler = handler;
                _servable = servableStateRoot;
            }

            private bool Servable(byte[] stateRoot) => ByteUtil.AreEqual(stateRoot, _servable);

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                if (!Servable(stateRoot))
                    throw new FetchRequestFailedException("account proof unavailable at expired/pruned root", null);
                AccountRangeCallsForServableRoot++;
                return await _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    StartingHash = startingHash,
                    LimitHash = limitHash,
                    ResponseBytes = responseBytes,
                }, ct).ConfigureAwait(false);
            }

            public async Task<StorageRangesMessage> FetchStorageRangesAsync(
                byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash,
                ulong responseBytes, CancellationToken ct)
            {
                if (!Servable(stateRoot))
                    throw new FetchRequestFailedException("storage range unavailable at expired/pruned root", null);
                StorageRangeCallsForServableRoot++;
                return await _handler.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    AccountHashes = accountHashes,
                    StartingHash = startingHash,
                    LimitHash = limitHash,
                    ResponseBytes = responseBytes,
                }, ct).ConfigureAwait(false);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class RawReadablePathNodeStore : ITrieNodeStore, IRawNodeReader
        {
            private readonly Dictionary<byte[], byte[]> _storage = new(new ByteArrayComparer());
            private readonly IHashProvider _hashProvider = new Sha3KeccackHashProvider();

            private static byte[] Key(Node node) => Key(node.Owner, node.Path);

            private static byte[] Key(byte[] owner, byte[] path)
            {
                path ??= new byte[0];
                if (owner == null || owner.Length == 0) return path;
                var key = new byte[owner.Length + path.Length];
                Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
                if (path.Length > 0) Buffer.BlockCopy(path, 0, key, owner.Length, path.Length);
                return key;
            }

            public void Commit(TrieNodeSet nodes)
            {
                if (nodes == null) return;
                foreach (var node in nodes.Nodes)
                {
                    var rlp = node.GetEncodedData();
                    if (rlp == null || rlp.Length < 32) continue;
                    _storage[Key(node)] = rlp;
                }
                foreach (var d in nodes.Deletes)
                {
                    if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                    _storage.Remove(Key(d.Owner, d.Path));
                }
            }

            public byte[] Get(Node reference)
            {
                if (reference == null) return null;
                if (!_storage.TryGetValue(Key(reference), out var blob) || blob == null) return null;
                if (!Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.TrieNode.Verify(reference.GetHash(), blob, _hashProvider))
                    throw new InvalidOperationException("Path-keyed trie node hash mismatch.");
                return blob;
            }

            public bool Contains(Node reference) => reference != null && _storage.ContainsKey(Key(reference));

            public bool ContainsKey(byte[] stateRoot)
            {
                if (stateRoot == null || stateRoot.Length != 32) return false;
                if (!_storage.TryGetValue(Key(null, new byte[0]), out var blob) || blob == null || blob.Length == 0) return false;
                return ByteUtil.AreEqual(_hashProvider.ComputeHash(blob), stateRoot);
            }

            public byte[] TryGetRawNode(byte[] owner, byte[] path)
                => _storage.TryGetValue(Key(owner, path), out var blob) ? blob : null;

            public void Flush() { }
            public void Clear() => _storage.Clear();
        }

        private sealed class StateTrieOverrideBundle : IChainStoreBundle
        {
            private readonly IChainStoreBundle _inner;
            private readonly ITrieNodeStore _stateTrieNodes;
            private readonly RecordingFlatStateStore _state;

            public StateTrieOverrideBundle(IChainStoreBundle inner, ITrieNodeStore stateTrieNodes)
            {
                _inner = inner;
                _stateTrieNodes = stateTrieNodes;
                _state = new RecordingFlatStateStore(inner.State);
            }

            public ITrieNodeStore StateTrieNodes => _stateTrieNodes;

            public RecordingFlatStateStore FlatRecorder => _state;

            public IStateStore State => _state;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }

        private sealed class RecordingFlatStateStore : IStateStore, ISnapFlatStateWriter
        {
            private readonly IStateStore _inner;
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _storageBySlotKeccak =
                new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();

            public RecordingFlatStateStore(IStateStore inner) => _inner = inner;

            public byte[] AccountHash { get; private set; }
            public int StorageWriteCount { get; private set; }
            public IReadOnlyDictionary<string, byte[]> StorageWrites => _storageBySlotKeccak;

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                AccountHash = accountHash;
                _storageBySlotKeccak[slotKeccak.ToHex()] = value;
                StorageWriteCount++;
                return Task.CompletedTask;
            }

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
                => (_inner as ISnapFlatStateWriter)?.SaveAccountByHashAsync(accountHash, account) ?? Task.CompletedTask;

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
                => (_inner as ISnapFlatStateWriter)?.GetAccountByHashAsync(accountHash) ?? Task.FromResult<Account>(null);

            public Task DeleteAccountByHashAsync(byte[] accountHash)
                => (_inner as ISnapFlatStateWriter)?.DeleteAccountByHashAsync(accountHash) ?? Task.CompletedTask;

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, System.Numerics.BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, System.Numerics.BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<System.Numerics.BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }
    }
}
