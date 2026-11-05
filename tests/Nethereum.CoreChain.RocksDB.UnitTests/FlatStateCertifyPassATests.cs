using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(DiskHeavyCertifyCollection.Name)]
    [Trait("Slice", "Flat")]
    public class FlatStateCertifyPassATests : IDisposable
    {
        private const int EoaCount = 2_000;
        private const int ContractCount = 3;
        private const int SlotsPerContract = 300;
        private const int LeafWalkBatchSize = 4_096;
        private const int MaxTrieDepth = 64;
        private const int UnitReferenceReads = 2 * 256;

        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"certifypassa_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            return bundle;
        }

        private static byte[] HashOf(string domain, int index)
            => Sha3Keccack.Current.CalculateHash(System.Text.Encoding.ASCII.GetBytes($"{domain}:{index}"));

        private static byte[] UnderNibble(byte[] key, int nibble)
        {
            var copy = (byte[])key.Clone();
            copy[0] = (byte)((nibble << 4) | (copy[0] & 0x0F));
            return copy;
        }

        private static int FirstNibble(byte[] key) => key[0] >> 4;

        private static byte[] SlotValue(int index) => ((BigInteger)(index + 1)).ToByteArray(isUnsigned: true, isBigEndian: true);

        private static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private sealed class SeededState
        {
            public List<byte[]> Eoas { get; }
            public List<byte[]> Contracts { get; }
            public List<byte[]> Slots { get; }
            public byte[] StorageRoot { get; }
            public byte[] Root { get; }

            public SeededState(int eoas, int contracts, int slotsPerContract, Func<byte[], byte[]> placeKey = null)
            {
                placeKey ??= key => key;
                Eoas = Enumerable.Range(0, eoas).Select(i => placeKey(HashOf("eoa", i))).ToList();
                Contracts = Enumerable.Range(0, contracts).Select(i => placeKey(HashOf("contract", i))).ToList();
                Slots = Enumerable.Range(0, slotsPerContract).Select(i => HashOf("slot", i)).ToList();
                var storage = new PatriciaTrie();
                for (int i = 0; i < Slots.Count; i++)
                    storage.Put(Slots[i], RLP.RLP.EncodeElement(SlotValue(i)));
                StorageRoot = Slots.Count == 0 ? DefaultValues.EMPTY_TRIE_HASH : storage.Root.GetHash();
                var accounts = new PatriciaTrie();
                for (int i = 0; i < Eoas.Count; i++)
                    accounts.Put(Eoas[i], new AccountEncoder().Encode(NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH)));
                foreach (var contract in Contracts)
                    accounts.Put(contract, new AccountEncoder().Encode(NewAccount(1, 10, StorageRoot)));
                Root = accounts.Root.GetHash();
            }

            public int AccountCount => Eoas.Count + Contracts.Count;

            public async Task SeedAsync(RocksDbChainStoreBundle bundle)
            {
                var flat = new RocksDbStateStore(bundle.Rocks);
                for (int i = 0; i < Eoas.Count; i++)
                    await flat.SaveAccountByHashAsync(Eoas[i], NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH));
                foreach (var contract in Contracts)
                {
                    await flat.SaveAccountByHashAsync(contract, NewAccount(1, 10, StorageRoot));
                    for (int i = 0; i < Slots.Count; i++)
                        await flat.SaveStorageByHashAsync(contract, Slots[i], SlotValue(i));
                }
                await bundle.GenerateTrieFromFlatAsync(Root, null, CancellationToken.None);
            }
        }

        private sealed class CountingPathNodeStore : ITrieNodeStore, IRawNodeReader, IContractStorageWipeable
        {
            private readonly ITrieNodeStore _inner;
            private long _gets;

            public CountingPathNodeStore(ITrieNodeStore inner) { _inner = inner; }

            public long Gets => Interlocked.Read(ref _gets);

            public byte[] Get(Node reference)
            {
                Interlocked.Increment(ref _gets);
                return _inner.Get(reference);
            }

            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => ((IRawNodeReader)_inner).TryGetRawNode(owner, path);
            public void DeleteRange(byte[] owner) => ((IContractStorageWipeable)_inner).DeleteRange(owner);
        }

        private static (FlatStateReconciler Reconciler, CountingPathNodeStore Store) CountingReconciler(RocksDbChainStoreBundle bundle)
        {
            var store = new CountingPathNodeStore(bundle.StateTrieNodes);
            return (new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), store), store);
        }

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

        private static void AssertNoLeafWalkMode(IEnumerable<string> lines)
            => Assert.DoesNotContain(lines, line => line.StartsWith("certify.mode=leafwalk", StringComparison.Ordinal));

        [Fact]
        public async Task Given_AFlatStateEqualToTheHealedTrie_When_Reconciled_Then_OnlyEachUnitsReferenceIsReadAndZeroRepairsAreReported()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var (reconciler, store) = CountingReconciler(bundle);
            var lines = new ConcurrentQueue<string>();

            var result = await reconciler.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.InRange(store.Gets, 1, UnitReferenceReads);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(state.AccountCount, result.AccountsScanned);
            Assert.Equal(ContractCount * SlotsPerContract, result.SlotsScanned);
            Assert.Equal(0, result.RangesWalked);
            Assert.Equal(0, result.LeavesWalked);
            Assert.Equal(0, result.TrieNodesMissing);
            AssertNoLeafWalkMode(lines);
            Assert.Equal(16, lines.Count(line => line.StartsWith("certify.A shard=", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task Given_OneGhostSlotInsideAContractsStorage_When_Reconciled_Then_OnlyThatOwnerIsLeafWalkedAndTheGhostIsDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var ghostSlot = new byte[32];
            ghostSlot[31] = 0x01;
            Assert.DoesNotContain(state.Slots, s => s.SequenceEqual(ghostSlot));
            var ghostRow = state.Contracts[1].Concat(ghostSlot).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x01 });
            var (reconciler, store) = CountingReconciler(bundle);
            var lines = new ConcurrentQueue<string>();

            var result = await reconciler.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.InRange(store.Gets, 1, UnitReferenceReads + MaxTrieDepth + LeafWalkBatchSize);
            Assert.Equal(1, result.GhostSlotsDeleted);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(SlotsPerContract, result.LeavesWalked);
            Assert.Equal(state.AccountCount, result.AccountsScanned);
            Assert.Equal(ContractCount * SlotsPerContract, result.SlotsScanned);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
            AssertNoLeafWalkMode(lines);
        }

        [Fact]
        public async Task Given_OneDivergedAccountInOneShard_When_Reconciled_Then_OnlyThatUnitsAccountsAreWalkedAndTheAccountIsPatched()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var diverged = state.Eoas[17];
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(diverged, NewAccount(999, 999, DefaultValues.EMPTY_TRIE_HASH));
            var unitAccounts = state.Eoas.Concat(state.Contracts).Count(a => a[0] == diverged[0]);
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(unitAccounts, result.LeavesWalked);
            Assert.Equal(state.AccountCount, result.AccountsScanned);
            Assert.Equal(ContractCount * SlotsPerContract, result.SlotsScanned);
            Assert.Equal((EvmUInt256)1, (await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(diverged)).Balance);
            Assert.Contains(lines, line => line.StartsWith($"snap.flat.certify.repair unit={diverged[0]:X2} ", StringComparison.Ordinal) && line.Contains(" range=accounts "));
        }

        [Fact]
        public async Task Given_StorageRowsWhoseOwnerHasNoAccountInFlatOrTrie_When_Reconciled_Then_OnlyThatOwnerIsWalkedAndTheRowsAreDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var orphan = HashOf("orphan", 0);
            Assert.DoesNotContain(state.Eoas.Concat(state.Contracts), a => a.SequenceEqual(orphan));
            var orphanRows = state.Slots.Take(3).Select(s => orphan.Concat(s).ToArray()).ToList();
            foreach (var row in orphanRows)
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, row, new byte[] { 0x07 });

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(3, result.GhostSlotsDeleted);
            Assert.Equal(3, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(0, result.LeavesWalked);
            Assert.Equal(ContractCount * SlotsPerContract, result.SlotsScanned);
            Assert.All(orphanRows, row => Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row)));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(40, 0)]
        public async Task Given_AccountsThatAllShareOneFirstNibble_When_Reconciled_Then_TheyAreProvenAndOnlyAUnitBelowALeafIsWalked(int accounts, int walkedUnits)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(accounts, 0, 0, key => UnderNibble(key, 0x5));
            await state.SeedAsync(bundle);
            var (reconciler, _) = CountingReconciler(bundle);
            var lines = new ConcurrentQueue<string>();

            var result = await reconciler.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(accounts, result.AccountsScanned);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(walkedUnits, result.RangesWalked);
        }

        [Fact]
        public async Task Given_TheRootNodeMissingFromTheStore_When_Certified_Then_EveryUnitReportsItAndItIsNotRegeneratedLocally()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            DeleteStoredNode(bundle, Array.Empty<byte>(), Array.Empty<byte>());
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.Length == 0);
            Assert.Contains(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal) && !line.Contains(" trie_missing=0 "));
            Assert.Null(((IRawNodeReader)bundle.StateTrieNodes).TryGetRawNode(Array.Empty<byte>(), Array.Empty<byte>()));
        }

        [Fact]
        public async Task Given_AnUndecodableFlatAccountRowAtAKeyTheTrieLacks_When_Certified_Then_ItsShardIsLeafWalkedAndTheRowIsDeletedAsAGhost()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var garbageKey = HashOf("garbage", 0);
            Assert.DoesNotContain(state.Eoas.Concat(state.Contracts), a => a.SequenceEqual(garbageKey));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, garbageKey, new byte[20].Concat(new byte[] { 0xC1, 0xFF }).ToArray());

            var verified = await bundle.VerifyFlatStateAsync(state.Root, null, CancellationToken.None);
            var reconciled = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, verified.GhostAccountsDeleted);
            Assert.Equal(1, verified.RangesWalked);
            Assert.Equal(1, reconciled.GhostAccountsDeleted);
            Assert.Equal(1, reconciled.TotalRepairs);
            Assert.Equal(state.AccountCount, reconciled.AccountsScanned);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, garbageKey));
        }

        [Fact]
        public async Task Given_AMalformedStorageKeyThatIsTheOwnerFollowedByAZeroByte_When_Certified_Then_TheOwnerWalkVisitsAndDeletesIt()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var malformed = state.Contracts[0].Concat(new byte[] { 0x00 }).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, malformed, new byte[] { 0x01 });

            var verified = await bundle.VerifyFlatStateAsync(state.Root, null, CancellationToken.None);
            var reconciled = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, verified.GhostSlotsDeleted);
            Assert.Equal(1, reconciled.GhostSlotsDeleted);
            Assert.Equal(1, reconciled.TotalRepairs);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, malformed));
        }

        public static IEnumerable<object[]> SinglePartitionShapes()
        {
            yield return new object[] { "leaf", 1 };
            yield return new object[] { "extension", 5 };
        }

        [Theory]
        [MemberData(nameof(SinglePartitionShapes))]
        public async Task Given_FlatWithOnePopulatedPartitionAndATrieWithTwo_When_TheNodeAtThatPartitionIsMissing_Then_CertifyReportsItUnresolvable(string shape, int accountsInPartition)
        {
            const int n = 0x2;
            const int m = 0x9;
            var bundle = OpenPathKeyedBundle();
            var sharedPrefix = new byte[] { 0x2A, 0xBB };
            var inPartition = Enumerable.Range(0, accountsInPartition).Select(i =>
            {
                var key = HashOf(shape, i);
                if (accountsInPartition > 1) Buffer.BlockCopy(sharedPrefix, 0, key, 0, sharedPrefix.Length);
                return UnderNibble(key, n);
            }).ToList();
            var others = Enumerable.Range(0, 3).Select(i => UnderNibble(HashOf("other", i), m)).ToList();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var trie = new PatriciaTrie();
            foreach (var key in inPartition.Concat(others))
            {
                await flat.SaveAccountByHashAsync(key, NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH));
                trie.Put(key, new AccountEncoder().Encode(NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH)));
            }
            var root = trie.Root.GetHash();
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            foreach (var key in others) bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, key);
            DeleteStoredNode(bundle, Array.Empty<byte>(), new[] { (byte)n });

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.VerifyFlatStateAsync(root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, node => node.Owner.Length == 0 && node.Path.SequenceEqual(new[] { (byte)n }));
            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None));
        }

        private const int LargeOwnerSlots = 100_000;
        private const int WhaleSlots = 2_000_000;

        private sealed class LargeOwnerState
        {
            public byte[] Owner { get; } = HashOf("whale", 0);
            public List<byte[]> SortedSlots { get; private set; }
            public byte[] StorageRoot { get; private set; }
            public byte[] Root { get; private set; }

            public static async Task<LargeOwnerState> SeedAsync(RocksDbChainStoreBundle bundle, int slotCount, Func<byte[], byte[]> placeSlot = null)
            {
                placeSlot ??= slot => slot;
                var state = new LargeOwnerState();
                state.SortedSlots = Enumerable.Range(0, slotCount).Select(i => placeSlot(HashOf("whale-slot", i))).Distinct(ByteArrayComparer.Current)
                    .OrderBy(s => s, ByteArrayComparer.Current).ToList();
                var rocks = bundle.Rocks;
                var flatStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
                var trieStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
                var trieAccounts = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                var batch = rocks.CreateWriteBatch();
                var pending = 0;
                void Put(byte[] key, byte[] value, RocksDbSharp.ColumnFamilyHandle cf)
                {
                    batch.Put(key, value, cf);
                    if (++pending < 20_000) return;
                    rocks.Write(batch);
                    batch.Dispose();
                    batch = rocks.CreateWriteBatch();
                    pending = 0;
                }

                var storage = new StackTrie(null, (path, blob) => Put(state.Owner.Concat(path).ToArray(), blob, trieStorage));
                for (int i = 0; i < state.SortedSlots.Count; i++)
                {
                    Put(state.Owner.Concat(state.SortedSlots[i]).ToArray(), SlotValue(i), flatStorage);
                    storage.Update(state.SortedSlots[i], RLP.RLP.EncodeElement(SlotValue(i)));
                }
                state.StorageRoot = storage.Hash();

                var accounts = new SortedDictionary<byte[], Account>(ByteArrayComparer.Current)
                {
                    [state.Owner] = NewAccount(1, 10, state.StorageRoot),
                };
                for (int i = 0; i < 64; i++)
                    accounts[HashOf("whale-eoa", i)] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
                var accountTrie = new StackTrie(null, (path, blob) => Put(path, blob, trieAccounts));
                foreach (var account in accounts)
                    accountTrie.Update(account.Key, new AccountEncoder().Encode(account.Value));
                state.Root = accountTrie.Hash();
                rocks.Write(batch);
                batch.Dispose();

                var flat = new RocksDbStateStore(rocks);
                foreach (var account in accounts)
                    await flat.SaveAccountByHashAsync(account.Key, account.Value);
                return state;
            }

            public byte[] StoredPathUnderNibble(RocksDbChainStoreBundle bundle, int nibble, int pathLength)
            {
                if (pathLength == 0) return Array.Empty<byte>();
                using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
                for (it.Seek(Owner.Concat(new[] { (byte)nibble }).ToArray()); it.Valid(); it.Next())
                {
                    var key = it.Key();
                    if (!key.Take(32).SequenceEqual(Owner) || key[32] != nibble) break;
                    if (key.Length == 32 + pathLength) return key.Skip(32).ToArray();
                }
                throw new InvalidOperationException($"no stored node of path length {pathLength} under nibble {nibble}");
            }
        }

        [Fact]
        public async Task Given_AStorageOwnerWithTwoMillionSlots_When_Certified_Then_ItsSixteenNibbleRangesAreProvenAgainstTheStoredRootAndNothingIsWalked()
        {
            var bundle = OpenPathKeyedBundle();
            var state = await LargeOwnerState.SeedAsync(bundle, WhaleSlots);
            var (reconciler, store) = CountingReconciler(bundle);

            var result = await reconciler.VerifyFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.InRange(store.Gets, 1, UnitReferenceReads + 16);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(0, result.RangesWalked);
            Assert.Equal(0, result.TrieNodesMissing);
            Assert.Equal(0, result.TrieNodesDiffering);
            Assert.Equal(WhaleSlots, result.SlotsScanned);
            Assert.Equal(65, result.AccountsScanned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Given_AMissingNodeInsideOneNibbleSubRangeOfALargeOwner_When_Certified_Then_TheMissingNodeIsReportedAndNotRegenerated(int pathLength)
        {
            var bundle = OpenPathKeyedBundle();
            var state = await LargeOwnerState.SeedAsync(bundle, LargeOwnerSlots);
            var holePath = state.StoredPathUnderNibble(bundle, 0x5, pathLength);
            DeleteStoredNode(bundle, state.Owner, holePath);
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(holePath));
            Assert.Null(((IRawNodeReader)bundle.StateTrieNodes).TryGetRawNode(state.Owner, holePath));
            Assert.Contains(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal) && !line.Contains(" trie_missing=0 "));
        }

        [Fact]
        public async Task Given_OneDivergedSlotInsideALargeOwner_When_Certified_Then_OnlyThatNibbleRangeIsWalkedAndTheSlotIsPatched()
        {
            var bundle = OpenPathKeyedBundle();
            var state = await LargeOwnerState.SeedAsync(bundle, LargeOwnerSlots);
            var divergedSlot = state.SortedSlots[LargeOwnerSlots / 2];
            var divergedRow = state.Owner.Concat(divergedSlot).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, divergedRow, new byte[] { 0xEE });
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(1, result.SlotsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(state.SortedSlots.Count(s => s[0] >> 4 == divergedSlot[0] >> 4), result.LeavesWalked);
            Assert.Equal(LargeOwnerSlots, result.SlotsScanned);
            Assert.Equal(SlotValue(LargeOwnerSlots / 2), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, divergedRow));
            Assert.Contains(lines, line => line.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && line.Contains($" range=nibble={divergedSlot[0] >> 4:x} "));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(2)]
        public async Task Given_ALargeOwnerOfExactly65536SlotsWhoseOnlySubTrieRootIsAnExtension_When_Certified_Then_TheWholeOwnerIsWalkedAndAHoleUnderItIsReported(int holePathLength)
        {
            const int boundarySlots = 65_536;
            var bundle = OpenPathKeyedBundle();
            var state = await LargeOwnerState.SeedAsync(bundle, boundarySlots, slot =>
            {
                var placed = (byte[])slot.Clone();
                placed[0] = 0x5A;
                return placed;
            });
            Assert.Equal(boundarySlots, state.SortedSlots.Count);
            var raw = (IRawNodeReader)bundle.StateTrieNodes;
            Assert.Null(raw.TryGetRawNode(state.Owner, new byte[] { 0x5 }));
            Assert.NotNull(raw.TryGetRawNode(state.Owner, new byte[] { 0x5, 0xA }));
            byte[] holePath = holePathLength switch { 0 => Array.Empty<byte>(), 2 => new byte[] { 0x5, 0xA }, _ => null };
            if (holePath != null) DeleteStoredNode(bundle, state.Owner, holePath);
            var lines = new ConcurrentQueue<string>();

            if (holePath == null)
            {
                var result = await bundle.VerifyFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);
                Assert.Equal(0, result.TotalRepairs);
                Assert.Equal(1, result.RangesWalked);
                Assert.Equal(boundarySlots, result.LeavesWalked);
                Assert.Equal(0, result.TrieNodesMissing);
                Assert.Equal(boundarySlots, result.SlotsScanned);
            }
            else
            {
                var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                    () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));
                Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(holePath));
                Assert.Null(raw.TryGetRawNode(state.Owner, holePath));
            }
        }

        [Fact]
        public async Task Given_AnyPathKeyedStore_When_Certified_Then_TwoHundredFiftySixAccountUnitsAreDrainedByMinOfCoresWorkers()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var lines = new ConcurrentQueue<string>();

            await bundle.VerifyFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Contains($"certify.queue units=256 workers={Math.Min(Environment.ProcessorCount, 256)}", lines);
        }

        public static IEnumerable<object[]> RandomStoreShapes()
        {
            foreach (var shape in new[] { "uniform", "one-first-nibble", "one-two-nibble-prefix", "two-units-one-partition", "single-account", "clustered-three-bytes", "one-account-per-partition" })
                foreach (var seed in new[] { 1, 2, 3 })
                    yield return new object[] { shape, seed };
        }

        private static byte[] PlaceAccount(string shape, Random random, int index)
        {
            var key = new byte[32];
            random.NextBytes(key);
            switch (shape)
            {
                case "one-first-nibble": key[0] = (byte)(0x70 | (key[0] & 0x0F)); break;
                case "one-two-nibble-prefix": key[0] = 0x57; break;
                case "two-units-one-partition": key[0] = index % 2 == 0 ? (byte)0x57 : (byte)0x59; break;
                case "clustered-three-bytes": key[0] = 0xC3; key[1] = 0x3C; key[2] = (byte)(index % 3); break;
                case "one-account-per-partition": key[0] = (byte)((index % 16) << 4 | (key[0] & 0x0F)); break;
            }
            return key;
        }

        [Theory]
        [MemberData(nameof(RandomStoreShapes))]
        public async Task Given_ARandomStoreOfAnyShape_When_CertifiedThroughTheUnitQueue_Then_EveryUnitIsProvenWithZeroRepairs(string shape, int seed)
        {
            var bundle = OpenPathKeyedBundle();
            var random = new Random(seed * 7919 + shape.Length);
            var count = shape switch { "single-account" => 1, "one-account-per-partition" => 16, "uniform" => 600, _ => 40 + random.Next(80) };
            var flat = new RocksDbStateStore(bundle.Rocks);
            var trie = new PatriciaTrie();
            var keys = new HashSet<byte[]>(ByteArrayComparer.Current);
            for (int i = 0; keys.Count < count; i++)
            {
                var key = PlaceAccount(shape, random, i);
                if (!keys.Add(key)) continue;
                var storageRoot = DefaultValues.EMPTY_TRIE_HASH;
                if (random.Next(4) == 0)
                {
                    var storage = new PatriciaTrie();
                    for (int slot = 0; slot < 1 + random.Next(30); slot++)
                    {
                        var slotKey = HashOf($"{shape}-{seed}-{i}", slot);
                        storage.Put(slotKey, RLP.RLP.EncodeElement(SlotValue(slot)));
                        await flat.SaveStorageByHashAsync(key, slotKey, SlotValue(slot));
                    }
                    storageRoot = storage.Root.GetHash();
                }
                var account = NewAccount((ulong)i, (ulong)random.Next(1, 1000), storageRoot);
                await flat.SaveAccountByHashAsync(key, account);
                trie.Put(key, new AccountEncoder().Encode(account));
            }
            var root = trie.Root.GetHash();
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            var (reconciler, _) = CountingReconciler(bundle);
            var lines = new ConcurrentQueue<string>();

            var result = await reconciler.VerifyFlatStateAsync(root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(count, result.AccountsScanned);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(0, result.TrieNodesMissing);
            Assert.Equal(0, result.TrieNodesDiffering);
            Assert.Contains(lines, line => line.StartsWith("certify.queue units=256 ", StringComparison.Ordinal));
        }

        private static byte[] StoredAccountPathOfLength(RocksDbChainStoreBundle bundle, int pathLength)
        {
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            for (it.SeekToFirst(); it.Valid(); it.Next())
                if (it.Key().Length == pathLength) return it.Key();
            throw new InvalidOperationException($"no stored account node of path length {pathLength}");
        }

        [Theory]
        [InlineData(0, "missing")]
        [InlineData(1, "missing")]
        [InlineData(2, "missing")]
        [InlineData(3, "missing")]
        [InlineData(0, "differ")]
        [InlineData(1, "differ")]
        [InlineData(2, "differ")]
        [InlineData(3, "differ")]
        public async Task Given_AnAccountTrieNodeMissingOrDifferingAtTheRootPartitionUnitOrInnerLevel_When_Certified_Then_ItIsCountedAndReportedUnresolvable(int pathLength, string defect)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var path = StoredAccountPathOfLength(bundle, pathLength);
            if (defect == "missing")
            {
                DeleteStoredNode(bundle, Array.Empty<byte>(), path);
            }
            else
            {
                var stored = bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
                stored[stored.Length - 1] ^= 0x01;
                bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, stored);
            }
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.VerifyFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.SequenceEqual(path));
            Assert.Contains(lines, line => line.StartsWith("snap.flat.certify.hole owner=0x path=", StringComparison.Ordinal));
        }

        private sealed class SteppingClock : TimeProvider
        {
            private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

            public override DateTimeOffset GetUtcNow()
                => new DateTimeOffset(Interlocked.Add(ref _ticks, TimeSpan.FromSeconds(61).Ticks), TimeSpan.Zero);
        }

        [Fact]
        public async Task Given_ACertifyCancelledWhileTheUnitQueueDrains_When_Reconciled_Then_ItStopsWithoutConcluding()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new SeededState(EoaCount, ContractCount, SlotsPerContract);
            await state.SeedAsync(bundle);
            var ghostRow = state.Contracts[0].Concat(new byte[32]).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x01 });
            var reconciler = new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), bundle.StateTrieNodes, new SteppingClock());
            using var cts = new CancellationTokenSource();
            var lines = new ConcurrentQueue<string>();
            var unitsDoneLines = 0;
            void Progress(string line)
            {
                lines.Enqueue(line);
                if (line.StartsWith("certify.A units_done=", StringComparison.Ordinal) && Interlocked.Increment(ref unitsDoneLines) == 64)
                    cts.Cancel();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => reconciler.ReconcileFlatStateAsync(state.Root, Progress, cts.Token));

            Assert.True(Volatile.Read(ref unitsDoneLines) >= 64);
            Assert.DoesNotContain(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_AFlatStoreWithTheBinaryPackedAccountLayout_When_Reconciled_Then_ItReportsTheLeafWalkFallbackForTheLayout()
        {
            var bundle = OpenPathKeyedBundle();
            var eoa = HashOf("eoa", 0);
            var other = UnderNibble(HashOf("eoa", 1), (FirstNibble(eoa) + 8) % 16);
            var flat = new RocksDbStateStore(bundle.Rocks, accountLayout: BinaryPackedAccountLayout.Instance);
            await flat.SaveAccountByHashAsync(eoa, NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH));
            await flat.SaveAccountByHashAsync(other, NewAccount(2, 2, DefaultValues.EMPTY_TRIE_HASH));
            var trie = new PatriciaTrie();
            trie.Put(eoa, new AccountEncoder().Encode(NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH)));
            trie.Put(other, new AccountEncoder().Encode(NewAccount(2, 2, DefaultValues.EMPTY_TRIE_HASH)));
            await new FlatStateTrieGenerator(bundle.Rocks, flat, bundle.StateTrieNodes).GenerateAsync(trie.Root.GetHash(), null, CancellationToken.None);
            var lines = new ConcurrentQueue<string>();

            var result = await new FlatStateReconciler(bundle.Rocks, flat, bundle.StateTrieNodes)
                .ReconcileFlatStateAsync(trie.Root.GetHash(), lines.Enqueue, CancellationToken.None);

            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Contains("certify.mode=leafwalk reason=binary_flat_layout", lines);
            Assert.DoesNotContain(lines, line => line.StartsWith("certify.accounts ", StringComparison.Ordinal));
        }
    }
}
