using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class SnapBootstrapperDeferredDebtHealE2ETests : IDisposable
    {
        private readonly string _dbPath;
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();

        public SnapBootstrapperDeferredDebtHealE2ETests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_deferredheal_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
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

        private sealed class HandlerBackedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            public int AccountRangeCalls;
            public int StorageRangeCalls;
            public int TrieNodeCalls;

            public HandlerBackedScheduler(PatriciaSnapRequestHandler handler) => _handler = handler;

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref AccountRangeCalls);
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
                Interlocked.Increment(ref StorageRangeCalls);
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

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref TrieNodeCalls);
                return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    Paths = paths,
                    ResponseBytes = responseBytes,
                }, ct).ConfigureAwait(false);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private static readonly byte[] AccountHashSeed = { 0x0A, 0xAA };

        private static byte[] FindDecoyAccountHash(byte[] accountHash)
        {
            byte seed = 0;
            byte[] decoy;
            do { decoy = Keccak.CalculateHash(new byte[] { 0x0D, 0xEC, seed++ }); }
            while (decoy[0] >> 4 == accountHash[0] >> 4);
            return decoy;
        }

        private static byte[] WriteAccountsTrieLocally(
            ITrieNodeStore store, byte[] accountHash, byte[] storageRoot, byte[] decoyAccountHash)
        {
            var trie = new PatriciaTrie(store, Hp);
            trie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            trie.Put(decoyAccountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1UL,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            trie.SaveDirtyNodesToStorageAndCollapse();
            return trie.Root.GetHash();
        }

        private static (InMemoryContentNodeStore Server, byte[] StateRoot, byte[] AccountHash, byte[] DecoyAccountHash,
            byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Slots) BuildWhaleServerState(int slotCount)
        {
            var server = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(server, Hp);
            var slots = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < slotCount; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { (byte)(i >> 8), (byte)(i & 0xFF), 0x77 });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xEE, (byte)(i & 0xFF), (byte)((i >> 4) & 0xFF) });
                slots.Add((slotHash, value));
                storageTrie.Put(slotHash, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountHash = Keccak.CalculateHash(AccountHashSeed);
            var decoyAccountHash = FindDecoyAccountHash(accountHash);
            var stateRoot = WriteAccountsTrieLocally(server, accountHash, storageRoot, decoyAccountHash);

            slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));
            return (server, stateRoot, accountHash, decoyAccountHash, storageRoot, slots);
        }

        private static BlockHeader Pivot(byte[] stateRoot) => new BlockHeader
        {
            BlockNumber = 1,
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

        private static List<(byte[] Key, byte[] Value)> ReadRecoveredStorage(
            ITrieNodeStore stateTrieNodes, byte[] accountHash, byte[] storageRoot)
        {
            var trie = PatriciaTrie.LoadFromStorage(storageRoot, stateTrieNodes, accountHash);
            var result = PatriciaRangeIterator.EnumerateRange(trie.Root, stateTrieNodes, new byte[32])
                .Select(e => (e.KeyBytes, e.Value))
                .ToList();
            result.Sort((a, b) => ByteArrayComparer.Current.Compare(a.KeyBytes, b.KeyBytes));
            return result;
        }

        private static SortedDictionary<string, string> ReadFlatStorage(RocksDbChainStoreBundle bundle, byte[] accountHash)
        {
            var flat = new SortedDictionary<string, string>(StringComparer.Ordinal);
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            it.Seek(accountHash);
            while (it.Valid())
            {
                var key = it.Key();
                if (key.Length != 64 || !ByteUtil.StartsWith(key, accountHash)) break;
                flat[key.Skip(32).ToArray().ToHex()] = it.Value().ToHex();
                it.Next();
            }
            return flat;
        }

        private static SortedDictionary<string, string> ExpectedFlatStorage(List<(byte[] Key, byte[] Value)> slots)
        {
            var expected = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in slots)
                expected[key.ToHex()] = Nethereum.RLP.RLP.Decode(value).RLPData.TrimZeroBytes().ToHex();
            return expected;
        }

        private static void AssertTrieHoldsExactly(RocksDbChainStoreBundle bundle, byte[] accountHash, byte[] storageRoot, List<(byte[] Key, byte[] Value)> slots)
        {
            var recovered = ReadRecoveredStorage(bundle.StateTrieNodes, accountHash, storageRoot);
            Assert.Equal(slots.Count, recovered.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                Assert.Equal(slots[i].Key.ToHex(), recovered[i].Key.ToHex());
                Assert.Equal(slots[i].Value.ToHex(), recovered[i].Value.ToHex());
            }
        }

        private static void SavePhase3State(RocksDbChainStoreBundle bundle, byte[] pivotHash, byte[] stateRoot, SnapSyncAccountTask[] tasks)
            => bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = pivotHash,
                HealTargetRoot = stateRoot,
                Tasks = tasks,
                Counters = SnapSyncCounters.Zero,
            });

        private static DeferredStorageDebt OpenDebt(byte[] accountHash, byte[] storageRoot, byte[] stateRoot, DeferredStorageReason reason)
            => new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = storageRoot,
                FetchStateRoot = stateRoot,
                FetchPivotBlock = 1UL,
                Reason = reason,
                Status = StorageCompleteness.DeferredBigAccount,
            };

        private static SnapRunOptions HealOnlyOptions(HandlerBackedScheduler scheduler, bool enableFlatReconcile = false)
            => new SnapRunOptions
            {
                Scheduler = scheduler,
                PivotRefresher = (_, _) => Task.FromResult<(BlockHeader Header, byte[] Hash)?>(null),
                RunBackfill = false,
                EnableFlatReconcile = enableFlatReconcile,
            };

        [Fact]
        public async Task Given_AnOpenDebtWithNoLocalStorage_When_Phase3HealRuns_Then_TheSeededHealBuildsTheWholeTrieAndFlatWithoutStorageRanges()
        {
            var (server, stateRoot, accountHash, decoyAccountHash, storageRoot, slots) = BuildWhaleServerState(slotCount: 250);
            var scheduler = new HandlerBackedScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes(), softResponseLimit: 400));

            using var bundle = RocksDbChainStoreBundle.Open(
                _dbPath, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            Assert.Equal(stateRoot.ToHex(), WriteAccountsTrieLocally(bundle.StateTrieNodes, accountHash, storageRoot, decoyAccountHash).ToHex());

            var pivotHeader = Pivot(stateRoot);
            var pivotHash = Keccak.CalculateHash(new byte[] { 0xFE, 0xED, 0x01 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);
            SavePhase3State(bundle, pivotHash, stateRoot, Array.Empty<SnapSyncAccountTask>());
            bundle.Metadata.UpsertDeferredStorageDebt(OpenDebt(accountHash, storageRoot, stateRoot, DeferredStorageReason.BigAccountSubrangeFailed));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance, HealOnlyOptions(scheduler), cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Equal(0, scheduler.StorageRangeCalls);
            Assert.True(scheduler.TrieNodeCalls > 0, "the seeded heal must have fetched the storage trie nodes");
            AssertTrieHoldsExactly(bundle, accountHash, storageRoot, slots);
            Assert.Equal(ExpectedFlatStorage(slots), ReadFlatStorage(bundle, accountHash));
        }

        [Fact]
        public async Task Given_DebtWithStorageAlreadyPresent_When_Phase3HealRuns_Then_NothingIsRefetched()
        {
            var (server, stateRoot, accountHash, _, storageRoot, slots) = BuildWhaleServerState(slotCount: 60);
            var scheduler = new HandlerBackedScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes()));

            using var bundle = RocksDbChainStoreBundle.Open(
                _dbPath, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });

            var preRanged = new PatriciaTrie(bundle.StateTrieNodes, accountHash);
            foreach (var (key, value) in slots) preRanged.Put(key, value);
            preRanged.SaveDirtyNodesToStorageAndCollapse();
            Assert.Equal(storageRoot.ToHex(), preRanged.Root.GetHash().ToHex());

            var pivotHeader = Pivot(stateRoot);
            var pivotHash = Keccak.CalculateHash(new byte[] { 0xFE, 0xED, 0x02 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);
            SavePhase3State(bundle, pivotHash, stateRoot, new[]
            {
                new SnapSyncAccountTask
                {
                    Next = new byte[32],
                    Last = SnapHashRanges.FilledHash(0xff),
                    StorageCompleted = new[] { accountHash },
                    SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                },
            });
            bundle.Metadata.UpsertDeferredStorageDebt(OpenDebt(accountHash, storageRoot, stateRoot, DeferredStorageReason.BigAccountSubrangeFailed));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance, HealOnlyOptions(scheduler), cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Equal(0, scheduler.StorageRangeCalls);
            AssertTrieHoldsExactly(bundle, accountHash, storageRoot, slots);
        }

        [Fact]
        public async Task Given_AnInterruptedPartialStorageTrie_When_Phase3HealRuns_Then_TheSeededHealCompletesItWithoutStorageRanges()
        {
            var (server, stateRoot, accountHash, _, storageRoot, slots) = BuildWhaleServerState(slotCount: 250);
            var scheduler = new HandlerBackedScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes(), softResponseLimit: 400));

            using var bundle = RocksDbChainStoreBundle.Open(
                _dbPath, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });

            var partial = new PatriciaTrie(bundle.StateTrieNodes, accountHash);
            foreach (var (key, value) in slots.Take(30)) partial.Put(key, value);
            partial.SaveDirtyNodesToStorageAndCollapse();
            Assert.NotEqual(storageRoot.ToHex(), partial.Root.GetHash().ToHex());

            var pivotHeader = Pivot(stateRoot);
            var pivotHash = Keccak.CalculateHash(new byte[] { 0xFE, 0xED, 0x03 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);
            SavePhase3State(bundle, pivotHash, stateRoot, Array.Empty<SnapSyncAccountTask>());
            bundle.Metadata.UpsertDeferredStorageDebt(OpenDebt(accountHash, storageRoot, stateRoot, DeferredStorageReason.BigAccountSubrangeFailed));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance, HealOnlyOptions(scheduler), cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Equal(0, scheduler.StorageRangeCalls);
            Assert.True(scheduler.TrieNodeCalls > 0, "the seeded heal must have fetched the missing storage trie nodes");
            AssertTrieHoldsExactly(bundle, accountHash, storageRoot, slots);
        }

        [Fact]
        public async Task Given_AnOpenBigAccountChunkedDebtForAMixedRootPartialWhale_When_Phase3HealRunsWithFlatReconcile_Then_TheTrieIsTheExactFinalRootAndFlatHoldsExactlyTheFinalSlots()
        {
            var (server, stateRoot, accountHash, _, storageRoot, slots) = BuildWhaleServerState(slotCount: 250);
            var scheduler = new HandlerBackedScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes(), softResponseLimit: 400));

            using var bundle = RocksDbChainStoreBundle.Open(
                _dbPath, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            var flatWriter = Assert.IsAssignableFrom<ISnapFlatStateWriter>(bundle.State);

            var deletedSinceOlderRoot = Keccak.CalculateHash(new byte[] { 0x5A, 0x1E });
            Assert.DoesNotContain(slots, s => ByteArrayComparer.Current.Equals(s.Key, deletedSinceOlderRoot));
            var olderRootPages = slots.Take(30).ToList();
            var changedSinceOlderRoot = olderRootPages[7].Key;
            olderRootPages[7] = (changedSinceOlderRoot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x01, 0xD0 }));
            olderRootPages.Add((deletedSinceOlderRoot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xDE, 0xAD })));

            var partial = new PatriciaTrie(bundle.StateTrieNodes, accountHash);
            foreach (var (key, value) in olderRootPages)
            {
                partial.Put(key, value);
                await flatWriter.SaveStorageByHashAsync(accountHash, key, Nethereum.RLP.RLP.Decode(value).RLPData);
            }
            partial.SaveDirtyNodesToStorageAndCollapse();
            Assert.NotEqual(storageRoot.ToHex(), partial.Root.GetHash().ToHex());

            var pivotHeader = Pivot(stateRoot);
            var pivotHash = Keccak.CalculateHash(new byte[] { 0xFE, 0xED, 0x04 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);
            SavePhase3State(bundle, pivotHash, stateRoot, Array.Empty<SnapSyncAccountTask>());
            bundle.Metadata.UpsertDeferredStorageDebt(OpenDebt(accountHash, storageRoot, stateRoot, DeferredStorageReason.BigAccountChunked));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance,
                HealOnlyOptions(scheduler, enableFlatReconcile: true), cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Equal(0, scheduler.StorageRangeCalls);
            AssertTrieHoldsExactly(bundle, accountHash, storageRoot, slots);

            var flat = ReadFlatStorage(bundle, accountHash);
            Assert.False(flat.ContainsKey(deletedSinceOlderRoot.ToHex()), "the slot deleted since the older root must not survive in flat");
            Assert.Equal(ExpectedFlatStorage(slots), flat);
        }
    }
}
