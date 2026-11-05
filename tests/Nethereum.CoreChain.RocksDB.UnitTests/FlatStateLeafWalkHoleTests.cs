using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class FlatStateLeafWalkHoleTests : IDisposable
    {
        private const int ContractSlotCount = 300;
        private const int AccountCount = 2_000;

        private readonly List<string> _dirs = new();
        private readonly List<RocksDbChainStoreBundle> _bundles = new();

        public void Dispose()
        {
            foreach (var bundle in _bundles) bundle.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"leafwalkhole_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _bundles.Add(bundle);
            return bundle;
        }

        private static byte[] HashOf(string domain, int index)
            => Sha3Keccack.Current.CalculateHash(System.Text.Encoding.ASCII.GetBytes($"{domain}:{index}"));

        private static byte[] SlotValue(int index) => ((BigInteger)(index + 1)).ToByteArray(isUnsigned: true, isBigEndian: true);

        private static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private static int Nibble(byte[] key, int index) => index % 2 == 0 ? key[index / 2] >> 4 : key[index / 2] & 0x0F;

        private static byte[] StorageRowKey(byte[] owner, byte[] slot) => owner.Concat(slot).ToArray();

        private static string ColumnChecksum(RocksDbManager rocks, string column)
        {
            var hasher = new List<byte>();
            using var it = rocks.CreateIterator(column);
            for (it.SeekToFirst(); it.Valid(); it.Next())
            {
                hasher.AddRange(it.Key());
                hasher.AddRange(it.Value());
            }
            return Sha3Keccack.Current.CalculateHash(hasher.ToArray()).ToHex();
        }

        private sealed class LeafWalkOnlyStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public LeafWalkOnlyStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Get(reference);
            public bool Contains(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private static FlatStateReconciler LeafWalkReconciler(RocksDbChainStoreBundle bundle)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new LeafWalkOnlyStore(bundle.StateTrieNodes));

        private static string FlatChecksum(RocksDbManager rocks)
            => ColumnChecksum(rocks, RocksDbManager.CF_STATE_ACCOUNTS) + ColumnChecksum(rocks, RocksDbManager.CF_STATE_STORAGE);

        private static void DeleteStoredNode(RocksDbChainStoreBundle bundle, byte[] owner, byte[] path)
        {
            var raw = (IRawNodeReader)bundle.StateTrieNodes;
            var stored = raw.TryGetRawNode(owner, path);
            Assert.NotNull(stored);
            var set = new TrieNodeSet();
            set.AddDelete(owner, path, stored);
            bundle.StateTrieNodes.Commit(set);
            Assert.Null(raw.TryGetRawNode(owner, path));
        }

        private sealed class ContractState
        {
            public byte[] Contract { get; } = HashOf("contract", 0);
            public byte[] Eoa { get; } = HashOf("eoa", 0);
            public List<byte[]> Slots { get; }
            public byte[] StorageRoot { get; }
            public byte[] Root { get; }

            public ContractState(int slotCount = ContractSlotCount)
            {
                Slots = Enumerable.Range(0, slotCount).Select(i => HashOf("slot", i)).ToList();
                var storage = new PatriciaTrie();
                for (int i = 0; i < Slots.Count; i++)
                    storage.Put(Slots[i], RLP.RLP.EncodeElement(SlotValue(i)));
                StorageRoot = storage.Root.GetHash();
                var accounts = new PatriciaTrie();
                accounts.Put(Contract, new AccountEncoder().Encode(NewAccount(1, 10, StorageRoot)));
                accounts.Put(Eoa, new AccountEncoder().Encode(NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH)));
                Root = accounts.Root.GetHash();
            }

            public async Task SeedAsync(RocksDbChainStoreBundle bundle)
            {
                var flat = new RocksDbStateStore(bundle.Rocks);
                await flat.SaveAccountByHashAsync(Contract, NewAccount(1, 10, StorageRoot));
                await flat.SaveAccountByHashAsync(Eoa, NewAccount(2, 20, DefaultValues.EMPTY_TRIE_HASH));
                for (int i = 0; i < Slots.Count; i++)
                    await flat.SaveStorageByHashAsync(Contract, Slots[i], SlotValue(i));
                await bundle.GenerateTrieFromFlatAsync(Root, null, CancellationToken.None);
            }

            public byte[] HolePath(RocksDbChainStoreBundle bundle)
            {
                var raw = (IRawNodeReader)bundle.StateTrieNodes;
                for (int nibble = 15; nibble >= 0; nibble--)
                {
                    var path = new[] { (byte)nibble };
                    if (raw.TryGetRawNode(Contract, path) is { Length: >= 32 }
                        && Slots.Any(s => Nibble(s, 0) < nibble))
                        return path;
                }
                throw new InvalidOperationException("no interior storage node to remove");
            }

            public void DivergeFlatUnder(RocksDbChainStoreBundle bundle, byte[] holePath, out byte[] patchedRow, out byte[] ghostRow)
            {
                var under = Slots.Where(s => Nibble(s, 0) == holePath[0]).OrderBy(s => s, ByteArrayComparer.Current).ToList();
                patchedRow = StorageRowKey(Contract, under[0]);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, patchedRow, new byte[] { 0xEE });
                var ghostSlot = new byte[32];
                ghostSlot[0] = (byte)(holePath[0] << 4);
                ghostSlot[31] = 0x01;
                Assert.DoesNotContain(Slots, s => s.SequenceEqual(ghostSlot));
                ghostRow = StorageRowKey(Contract, ghostSlot);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x01 });
            }

            public void DivergeFlatBefore(RocksDbChainStoreBundle bundle, byte[] holePath, out byte[] patchedRow, out byte[] ghostRow)
            {
                var below = Slots.Where(s => Nibble(s, 0) < holePath[0]).OrderBy(s => s, ByteArrayComparer.Current).First();
                patchedRow = StorageRowKey(Contract, below);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, patchedRow, new byte[] { 0xEE });
                var ghostSlot = new byte[32];
                ghostSlot[31] = 0x01;
                Assert.DoesNotContain(Slots, s => s.SequenceEqual(ghostSlot));
                ghostRow = StorageRowKey(Contract, ghostSlot);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x01 });
            }
        }

        private sealed class ManyAccountsState
        {
            public List<byte[]> Accounts { get; } = Enumerable.Range(0, AccountCount).Select(i => HashOf("account", i)).ToList();
            public byte[] Root { get; }

            public ManyAccountsState()
            {
                var accounts = new PatriciaTrie();
                for (int i = 0; i < Accounts.Count; i++)
                    accounts.Put(Accounts[i], new AccountEncoder().Encode(NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH)));
                Root = accounts.Root.GetHash();
            }

            public async Task SeedAsync(RocksDbChainStoreBundle bundle)
            {
                var flat = new RocksDbStateStore(bundle.Rocks);
                for (int i = 0; i < Accounts.Count; i++)
                    await flat.SaveAccountByHashAsync(Accounts[i], NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH));
                await bundle.GenerateTrieFromFlatAsync(Root, null, CancellationToken.None);
            }

            public byte[] HolePath(RocksDbChainStoreBundle bundle, int shard = 0x7)
            {
                var raw = (IRawNodeReader)bundle.StateTrieNodes;
                for (int nibble = 15; nibble >= 0; nibble--)
                {
                    var path = new[] { (byte)shard, (byte)nibble };
                    if (raw.TryGetRawNode(Array.Empty<byte>(), path) is { Length: >= 32 }
                        && Accounts.Any(a => Nibble(a, 0) == shard && Nibble(a, 1) < nibble))
                        return path;
                }
                throw new InvalidOperationException("no interior account node to remove");
            }

            public void DivergeFlatBefore(RocksDbChainStoreBundle bundle, byte[] holePath, out byte[] patchedAccount, out byte[] ghostAccount)
            {
                patchedAccount = Accounts
                    .Where(a => Nibble(a, 0) == holePath[0] && Nibble(a, 1) < holePath[1])
                    .OrderBy(a => a, ByteArrayComparer.Current).First();
                var flat = new RocksDbStateStore(bundle.Rocks);
                flat.SaveAccountByHashAsync(patchedAccount, NewAccount(999, 999, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
                ghostAccount = new byte[32];
                ghostAccount[0] = (byte)(holePath[0] << 4);
                ghostAccount[31] = 0x01;
                var ghost = ghostAccount;
                Assert.DoesNotContain(Accounts, a => a.SequenceEqual(ghost));
                flat.SaveAccountByHashAsync(ghostAccount, NewAccount(5, 5, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            }
        }

        [Fact]
        public async Task Given_a_partially_healed_trie_When_the_flat_reconcile_walk_runs_Then_it_reports_the_missing_node_instead_of_skipping_leaves()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            DeleteStoredNode(bundle, state.Contract, holePath);
            var before = FlatChecksum(bundle.Rocks);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => LeafWalkReconciler(bundle).ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Equal(state.Contract, missing.Owner);
            Assert.Equal(holePath, missing.Path);
            Assert.Equal(before, FlatChecksum(bundle.Rocks));
        }

        [Fact]
        public async Task Given_the_same_trie_fully_healed_When_the_flat_reconcile_walk_runs_Then_it_scans_every_leaf_and_reports_nothing_missing()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);

            var result = await LeafWalkReconciler(bundle).ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(ContractSlotCount, result.SlotsScanned);
            Assert.Equal(0, result.TotalRepairs);
        }

        [Fact]
        public async Task Given_AMissingInteriorNodeInsideARangeWhoseFlatAlsoDiffers_When_Reconciled_Then_ItThrowsUnresolvableWithThatOwnerAndPathAndTheBatchWritesNothing()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            DeleteStoredNode(bundle, state.Contract, holePath);
            state.DivergeFlatUnder(bundle, holePath, out var patchedRow, out var ghostRow);
            var before = FlatChecksum(bundle.Rocks);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            var subtree = Assert.Single(thrown.Subtrees);
            Assert.Equal(state.Contract, subtree.AccountHash);
            Assert.Equal(state.StorageRoot, subtree.StorageRoot);
            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Equal(state.Contract, missing.Owner);
            Assert.Equal(holePath, missing.Path);
            Assert.Equal(before, FlatChecksum(bundle.Rocks));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, patchedRow));
            Assert.Equal(new byte[] { 0x01 }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
        }

        [Fact]
        public async Task Given_ALeafWalkFallbackAndAMissingInteriorNodeInsideAnOwnerWhoseFlatDiffersElsewhere_When_Reconciled_Then_TheOwnersBatchWritesNothing()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            DeleteStoredNode(bundle, state.Contract, holePath);
            state.DivergeFlatBefore(bundle, holePath, out var patchedRow, out var ghostRow);
            var before = FlatChecksum(bundle.Rocks);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => LeafWalkReconciler(bundle).ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Equal(state.Contract, missing.Owner);
            Assert.Equal(holePath, missing.Path);
            Assert.Equal(before, FlatChecksum(bundle.Rocks));
        }

        [Fact]
        public async Task Given_AMissingInteriorNodeAwayFromAnOwnersDivergedRows_When_Certified_Then_TheHoleIsReportedAndNotRegenerated()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            DeleteStoredNode(bundle, state.Contract, holePath);
            state.DivergeFlatBefore(bundle, holePath, out _, out _);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Contract) && m.Path.SequenceEqual(holePath));
            Assert.Null(((IRawNodeReader)bundle.StateTrieNodes).TryGetRawNode(state.Contract, holePath));
        }

        [Fact]
        public async Task Given_TheSameDivergedStorageFlatWithEveryTrieNodePresent_When_Reconciled_Then_TheOwnerIsRepairedAndNothingIsUnresolvable()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            state.DivergeFlatBefore(bundle, holePath, out var patchedRow, out var ghostRow);
            var before = FlatChecksum(bundle.Rocks);

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(ContractSlotCount, result.SlotsScanned);
            Assert.Equal(1, result.SlotsPatched);
            Assert.Equal(1, result.GhostSlotsDeleted);
            Assert.Equal(2, result.TotalRepairs);
            Assert.NotEqual(before, FlatChecksum(bundle.Rocks));
            Assert.NotEqual(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, patchedRow));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
        }

        [Fact]
        public async Task Given_AMissingAccountTrieUnitNodeAndDivergedFlatInEarlierUnitsOfItsShard_When_Reconciled_Then_ItReportsThatPathAndRepairsOnlyTheOtherUnits()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ManyAccountsState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            DeleteStoredNode(bundle, Array.Empty<byte>(), holePath);
            state.DivergeFlatBefore(bundle, holePath, out var patchedAccount, out var ghostAccount);
            var holedUnit = (byte)(holePath[0] << 4 | holePath[1]);
            var holedUnitFlat = AccountRowsInUnit(bundle, holedUnit);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Empty(thrown.Subtrees);
            Assert.NotEmpty(thrown.MissingNodes);
            Assert.All(thrown.MissingNodes, m =>
            {
                Assert.Empty(m.Owner);
                Assert.Equal(holePath, m.Path);
            });
            Assert.Equal(holedUnitFlat, AccountRowsInUnit(bundle, holedUnit));
            Assert.Equal((EvmUInt256)1, (await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(patchedAccount)).Balance);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, ghostAccount));
        }

        private static string AccountRowsInUnit(RocksDbChainStoreBundle bundle, byte unit)
        {
            var rows = new List<byte>();
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            for (it.Seek(new[] { unit }); it.Valid() && it.Key()[0] == unit; it.Next())
            {
                rows.AddRange(it.Key());
                rows.AddRange(it.Value());
            }
            return Convert.ToHexString(Sha3Keccack.Current.CalculateHash(rows.ToArray()));
        }

        [Fact]
        public async Task Given_TheSameDivergedAccountFlatWithEveryTrieNodePresent_When_Reconciled_Then_TheShardIsRepairedAndNothingIsUnresolvable()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ManyAccountsState();
            await state.SeedAsync(bundle);
            var holePath = state.HolePath(bundle);
            state.DivergeFlatBefore(bundle, holePath, out var patchedAccount, out var ghostAccount);

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(AccountCount, result.AccountsScanned);
            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.GhostAccountsDeleted);
            Assert.Equal(2, result.TotalRepairs);
            Assert.Equal((EvmUInt256)1, (await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(patchedAccount)).Balance);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, ghostAccount));
        }

        private const int LeafWalkBatchSize = 4_096;
        private const int TwoBatchSlotCount = 5_000;

        private sealed class TwoBatchOwner
        {
            public ContractState State { get; } = new ContractState(TwoBatchSlotCount);
            public List<int> SortedIndexes { get; }

            public TwoBatchOwner()
            {
                SortedIndexes = Enumerable.Range(0, State.Slots.Count)
                    .OrderBy(i => State.Slots[i], ByteArrayComparer.Current).ToList();
            }

            public byte[] SortedSlot(int position) => State.Slots[SortedIndexes[position]];

            public byte[] SecondBatchHolePath(RocksDbChainStoreBundle bundle)
            {
                var raw = (IRawNodeReader)bundle.StateTrieNodes;
                var firstOfSecondBatch = Nibble(SortedSlot(LeafWalkBatchSize), 0);
                for (int nibble = 15; nibble > firstOfSecondBatch; nibble--)
                {
                    var path = new[] { (byte)nibble };
                    var firstUnderPath = Enumerable.Range(0, SortedIndexes.Count).First(i => Nibble(SortedSlot(i), 0) == nibble);
                    if (firstUnderPath > LeafWalkBatchSize && raw.TryGetRawNode(State.Contract, path) is { Length: >= 32 })
                        return path;
                }
                throw new InvalidOperationException("no interior node wholly inside the second batch");
            }

            public void Diverge(RocksDbChainStoreBundle bundle, out byte[] firstBatchRow, out byte[] ghostRow, out byte[] secondBatchRow)
            {
                firstBatchRow = StorageRowKey(State.Contract, SortedSlot(0));
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, firstBatchRow, new byte[] { 0xEE });
                var ghostSlot = new byte[32];
                ghostSlot[31] = 0x01;
                Assert.True(ByteArrayComparer.Current.Compare(ghostSlot, SortedSlot(0)) < 0);
                ghostRow = StorageRowKey(State.Contract, ghostSlot);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x01 });
                secondBatchRow = StorageRowKey(State.Contract, SortedSlot(LeafWalkBatchSize));
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, secondBatchRow, new byte[] { 0xEE });
            }
        }

        [Fact]
        public async Task Given_AMissingNodeInsideTheSecondLeafBatchOfAFiveThousandSlotOwner_When_Reconciled_Then_TheFirstBatchIsRepairedTheHoledBatchWritesNothingAndTheOwnerIsNotCertifiedClean()
        {
            var bundle = OpenPathKeyedBundle();
            var owner = new TwoBatchOwner();
            await owner.State.SeedAsync(bundle);
            var holePath = owner.SecondBatchHolePath(bundle);
            DeleteStoredNode(bundle, owner.State.Contract, holePath);
            owner.Diverge(bundle, out var firstBatchRow, out var ghostRow, out var secondBatchRow);
            var leafWalk = LeafWalkReconciler(bundle);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => leafWalk.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None));

            var subtree = Assert.Single(thrown.Subtrees);
            Assert.Equal(owner.State.Contract, subtree.AccountHash);
            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Equal(owner.State.Contract, missing.Owner);
            Assert.Equal(holePath, missing.Path);
            Assert.Equal(SlotValue(owner.SortedIndexes[0]), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, firstBatchRow));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, secondBatchRow));
            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => leafWalk.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, secondBatchRow));
        }

        [Fact]
        public async Task Given_TheSameFiveThousandSlotOwnerHoleAndDivergence_When_Certified_Then_TheHoleIsReportedTheHoledBatchWritesNothingAndARerunAfterTheHealIsClean()
        {
            var bundle = OpenPathKeyedBundle();
            var owner = new TwoBatchOwner();
            await owner.State.SeedAsync(bundle);
            var holePath = owner.SecondBatchHolePath(bundle);
            var canonical = ((IRawNodeReader)bundle.StateTrieNodes).TryGetRawNode(owner.State.Contract, holePath);
            DeleteStoredNode(bundle, owner.State.Contract, holePath);
            owner.Diverge(bundle, out var firstBatchRow, out var ghostRow, out var secondBatchRow);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None));
            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner.State.Contract) && m.Path.SequenceEqual(holePath));
            Assert.Null(((IRawNodeReader)bundle.StateTrieNodes).TryGetRawNode(owner.State.Contract, holePath));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, secondBatchRow));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.State.Contract.Concat(holePath).ToArray(), canonical);

            var healed = await bundle.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None);

            Assert.Equal(SlotValue(owner.SortedIndexes[0]), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, firstBatchRow));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
            Assert.Equal(SlotValue(owner.SortedIndexes[LeafWalkBatchSize]), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, secondBatchRow));
            Assert.True(healed.TotalRepairs > 0);
            Assert.Equal(0, (await bundle.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None)).TotalRepairs);
        }

        [Fact]
        public async Task Given_TheSameTwoBatchDivergenceWithEveryTrieNodePresent_When_Reconciled_Then_BothBatchesAreRepaired()
        {
            var bundle = OpenPathKeyedBundle();
            var owner = new TwoBatchOwner();
            await owner.State.SeedAsync(bundle);
            owner.Diverge(bundle, out var firstBatchRow, out var ghostRow, out var secondBatchRow);

            var result = await bundle.ReconcileFlatStateAsync(owner.State.Root, null, CancellationToken.None);

            Assert.Equal(TwoBatchSlotCount, result.SlotsScanned);
            Assert.Equal(2, result.SlotsPatched);
            Assert.Equal(1, result.GhostSlotsDeleted);
            Assert.Equal(SlotValue(owner.SortedIndexes[0]), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, firstBatchRow));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
            Assert.Equal(SlotValue(owner.SortedIndexes[LeafWalkBatchSize]), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, secondBatchRow));
        }

        [Fact]
        public async Task Given_AMissingAccountTrieRootSeenByEveryShard_When_Reconciled_Then_ItIsReportedAsOneMissingNode()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ContractState();
            await state.SeedAsync(bundle);
            DeleteStoredNode(bundle, Array.Empty<byte>(), Array.Empty<byte>());

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Empty(thrown.Subtrees);
            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Empty(missing.Owner);
            Assert.Empty(missing.Path);
        }

        [Fact]
        public async Task Given_MissingAccountTrieNodesInTwoShards_When_Reconciled_Then_BothDistinctPathsAreReported()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new ManyAccountsState();
            await state.SeedAsync(bundle);
            var first = state.HolePath(bundle, 0x3);
            var second = state.HolePath(bundle, 0x7);
            DeleteStoredNode(bundle, Array.Empty<byte>(), first);
            DeleteStoredNode(bundle, Array.Empty<byte>(), second);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Equal(2, thrown.MissingNodes.Count);
            Assert.Contains(thrown.MissingNodes, m => m.Path.SequenceEqual(first));
            Assert.Contains(thrown.MissingNodes, m => m.Path.SequenceEqual(second));
        }
    }
}
