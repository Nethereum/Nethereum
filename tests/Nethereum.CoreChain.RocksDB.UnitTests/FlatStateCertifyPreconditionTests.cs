using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class FlatStateCertifyPreconditionTests : IDisposable
    {
        private const string LeafWalkFallback = "certify.mode=leafwalk reason=no_path_store";
        private const string ContractAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string EoaAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private string NewDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"certifypre_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            return dir;
        }

        private RocksDbChainStoreBundle OpenBundle(bool pathKeyed)
        {
            var bundle = RocksDbChainStoreBundle.Open(NewDir(), storageOptions: new RocksDbStorageOptions
            {
                PathKeyedState = pathKeyed,
                TrieNodeHistoryBlocks = pathKeyed ? 128 : -1,
            });
            _owned.Add(bundle);
            return bundle;
        }

        private static byte[] SlotValue(int slot) => ((BigInteger)slot).ToByteArray(isUnsigned: true, isBigEndian: true);

        private static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private sealed class TwoAccountState
        {
            public int SlotCount { get; }
            public byte[] ContractHash { get; } = StateKeys.AccountKey(ContractAddress);
            public byte[] EoaHash { get; } = StateKeys.AccountKey(EoaAddress);
            public byte[] StorageRoot { get; }
            public byte[] Root { get; }

            public TwoAccountState(int slotCount)
            {
                SlotCount = slotCount;
                var storage = new PatriciaTrie();
                foreach (var slot in Slots)
                    storage.Put(StateKeys.StorageSlotKey(slot), RLP.RLP.EncodeElement(SlotValue(slot)));
                StorageRoot = slotCount == 0 ? DefaultValues.EMPTY_TRIE_HASH : storage.Root.GetHash();
                var accounts = new PatriciaTrie();
                accounts.Put(ContractHash, new AccountEncoder().Encode(NewAccount(1, 10, StorageRoot)));
                accounts.Put(EoaHash, new AccountEncoder().Encode(NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH)));
                Root = accounts.Root.GetHash();
            }

            public IEnumerable<int> Slots => Enumerable.Range(1, SlotCount);

            public byte[] ContractSlotRowKey(int slot) => ContractHash.Concat(StateKeys.StorageSlotKey(slot)).ToArray();

            public byte[] GhostRowKey => EoaHash.Concat(StateKeys.StorageSlotKey(7)).ToArray();

            public async Task WriteFlatAsync(RocksDbStateStore flat)
            {
                await flat.SaveAccountByHashAsync(ContractHash, NewAccount(1, 10, StorageRoot));
                await flat.SaveAccountByHashAsync(EoaHash, NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH));
                foreach (var slot in Slots)
                    await flat.SaveStorageByHashAsync(ContractHash, StateKeys.StorageSlotKey(slot), SlotValue(slot));
            }

            public FlatStateBatch ContractStorageBatch()
                => new FlatStateBatch(null, null,
                    Slots.Select(slot => (ContractAddress, (BigInteger)slot, SlotValue(slot))).ToList(),
                    null, null, null);
        }

        private static async Task<TwoAccountState> SeedTrieAsync(RocksDbChainStoreBundle bundle, int slotCount)
        {
            var state = new TwoAccountState(slotCount);
            await state.WriteFlatAsync(new RocksDbStateStore(bundle.Rocks));
            await bundle.GenerateTrieFromFlatAsync(state.Root, null, CancellationToken.None);
            return state;
        }

        private static void AssertSameSplit(FlatStateReconcileResult expected, FlatStateReconcileResult actual)
        {
            Assert.Equal(expected.AccountsScanned, actual.AccountsScanned);
            Assert.Equal(expected.SlotsScanned, actual.SlotsScanned);
            Assert.Equal(expected.GhostAccountsDeleted, actual.GhostAccountsDeleted);
            Assert.Equal(expected.GhostSlotsDeleted, actual.GhostSlotsDeleted);
            Assert.Equal(expected.AccountsAdded, actual.AccountsAdded);
            Assert.Equal(expected.SlotsAdded, actual.SlotsAdded);
            Assert.Equal(expected.AccountsPatched, actual.AccountsPatched);
            Assert.Equal(expected.SlotsPatched, actual.SlotsPatched);
        }

        [Fact]
        public async Task Given_ATrieNodeStoreWithoutPathAddressedReads_When_ReconciledAndVerified_Then_BothReportTheLeafWalkFallback()
        {
            var dir = NewDir();
            Directory.CreateDirectory(dir);
            var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            _owned.Add(rocks);
            var flat = new RocksDbStateStore(rocks);
            var trieStore = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(trieStore);
            var eoa = StateKeys.AccountKey(EoaAddress);
            trie.Put(eoa, new AccountEncoder().Encode(NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH)));
            trie.SaveNodesToStorage();
            await flat.SaveAccountByHashAsync(eoa, NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH));
            var reconciler = new FlatStateReconciler(rocks, flat, trieStore);
            var reconcileLines = new List<string>();
            var verifyLines = new List<string>();

            var reconciled = await reconciler.ReconcileFlatStateAsync(trie.Root.GetHash(), reconcileLines.Add, CancellationToken.None);
            var verified = await reconciler.VerifyFlatStateAsync(trie.Root.GetHash(), verifyLines.Add, CancellationToken.None);

            Assert.Equal(1, reconciled.AccountsScanned);
            Assert.Equal(0, verified.TotalRepairs);
            Assert.Equal(1, reconcileLines.Count(line => line == LeafWalkFallback));
            Assert.Equal(1, verifyLines.Count(line => line == LeafWalkFallback));
        }

        private class ForwardingNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public ForwardingNodeStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Get(reference);
            public bool Contains(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private sealed class RawReaderOnlyNodeStore : ForwardingNodeStore, IRawNodeReader
        {
            public RawReaderOnlyNodeStore(ITrieNodeStore inner) : base(inner) { }
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => throw new NotSupportedException();
        }

        private sealed class WipeableOnlyNodeStore : ForwardingNodeStore, IContractStorageWipeable
        {
            public WipeableOnlyNodeStore(ITrieNodeStore inner) : base(inner) { }
            public void DeleteRange(byte[] owner) => throw new NotSupportedException();
        }

        private sealed class BothCapabilitiesNodeStore : ForwardingNodeStore, IRawNodeReader, IContractStorageWipeable
        {
            public BothCapabilitiesNodeStore(ITrieNodeStore inner) : base(inner) { }
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => throw new NotSupportedException();
            public void DeleteRange(byte[] owner) => throw new NotSupportedException();
        }

        [Theory]
        [InlineData("raw-reader-only", 1)]
        [InlineData("wipeable-only", 1)]
        [InlineData("both", 0)]
        public async Task Given_ATrieNodeStoreWithOnlyOneOfThePathAddressedCapabilities_When_Reconciled_Then_ItReportsTheLeafWalkFallback(
            string capabilities, int expectedFallbackLines)
        {
            var dir = NewDir();
            Directory.CreateDirectory(dir);
            var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            _owned.Add(rocks);
            var flat = new RocksDbStateStore(rocks);
            ITrieNodeStore trieStore = capabilities == "both" ? new RocksDbPathTrieNodeStore(rocks) : new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(trieStore);
            var eoa = StateKeys.AccountKey(EoaAddress);
            trie.Put(eoa, new AccountEncoder().Encode(NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH)));
            trie.SaveNodesToStorage();
            await flat.SaveAccountByHashAsync(eoa, NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH));
            ITrieNodeStore store = capabilities switch
            {
                "raw-reader-only" => new RawReaderOnlyNodeStore(trieStore),
                "wipeable-only" => new WipeableOnlyNodeStore(trieStore),
                _ => new BothCapabilitiesNodeStore(trieStore),
            };
            var lines = new List<string>();

            var reconciled = await new FlatStateReconciler(rocks, flat, store)
                .ReconcileFlatStateAsync(trie.Root.GetHash(), lines.Add, CancellationToken.None);

            Assert.Equal(1, reconciled.AccountsScanned);
            Assert.Equal(0, reconciled.TotalRepairs);
            Assert.Equal(expectedFallbackLines, lines.Count(line => line == LeafWalkFallback));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task Given_TheBundleStoreChain_When_Reconciled_Then_OnlyAStoreWithoutPathAddressedReadsReportsTheLeafWalkFallback(
            bool pathKeyed, bool expectFallback)
        {
            var bundle = OpenBundle(pathKeyed);
            var state = await SeedTrieAsync(bundle, slotCount: 3);
            var lines = new List<string>();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, lines.Add, CancellationToken.None);

            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(expectFallback, bundle.StateTrieNodes is not (IRawNodeReader and IContractStorageWipeable));
            Assert.Equal(expectFallback ? 1 : 0, lines.Count(line => line == LeafWalkFallback));
        }

        [Fact]
        public async Task Given_ADirtyFlat_When_Verified_Then_ItDrainsReportsTheSameSplitAsReconcileAndWritesNothing()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var state = await SeedTrieAsync(bundle, slotCount: 20_000);
            foreach (var slot in state.Slots)
                bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, state.ContractSlotRowKey(slot));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey, new byte[] { 0x01 });

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(state.ContractStorageBatch(), 1, new byte[32]);
            var verified = await bundle.VerifyFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(0, verified.SlotsAdded);
            Assert.Equal(1, verified.GhostSlotsDeleted);
            Assert.Equal(1, verified.TotalRepairs);
            Assert.Equal(new byte[] { 0x01 }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey));

            var reconciled = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            AssertSameSplit(verified, reconciled);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey));
            Assert.Equal(20_000, reconciled.SlotsScanned);
        }

        [Fact]
        public async Task Given_ANodeCommitBlockContextWithACurrentBlock_When_Reconciled_Then_ItRefusesBeforeAnyReadOrWrite()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var state = await SeedTrieAsync(bundle, slotCount: 3);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey, new byte[] { 0x01 });
            bundle.NodeCommitBlockSource.Arm(5);
            var lines = new List<string>();

            var reconcile = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Add, CancellationToken.None));
            var verify = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.VerifyFlatStateAsync(state.Root, lines.Add, CancellationToken.None));

            Assert.Contains("CurrentBlock", reconcile.Message);
            Assert.Contains("CurrentBlock", verify.Message);
            Assert.Empty(lines);
            Assert.Equal(new byte[] { 0x01 }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_ACommittedFlatReconciledCleanAtARoot_When_AGhostAppearsAndItIsReconciledAgainAtTheSameRoot_Then_TheGhostIsDeleted(bool pathKeyed)
        {
            var bundle = OpenBundle(pathKeyed);
            var state = await SeedTrieAsync(bundle, slotCount: 3);
            bundle.Metadata.Commit(1, Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 }));
            var first = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);
            Assert.Equal(0, first.TotalRepairs);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey, new byte[] { 0x01 });

            var again = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, again.GhostSlotsDeleted);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey));
        }

        [Fact]
        public async Task Given_ANodeCommitBlockContextThatWasCleared_When_Reconciled_Then_ItRunsAndRepairs()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var state = await SeedTrieAsync(bundle, slotCount: 3);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey, new byte[] { 0x01 });
            bundle.NodeCommitBlockSource.Arm(5);
            bundle.NodeCommitBlockSource.Clear();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.GhostSlotsDeleted);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.GhostRowKey));
        }
    }
}
