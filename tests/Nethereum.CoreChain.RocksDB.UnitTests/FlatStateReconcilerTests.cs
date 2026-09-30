using System;
using System.IO;
using System.Threading;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatStateReconcilerTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"flatrec_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;
        private readonly RocksDbStateStore _flat;
        private readonly AccountEncoder _enc = new AccountEncoder();

        public FlatStateReconcilerTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _flat = new RocksDbStateStore(_mgr);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private static byte[] Hash32(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        private static byte[] ShardKey(byte leading, byte within)
        {
            var h = new byte[32];
            h[0] = leading;
            h[1] = within;
            return h;
        }

        private static Account MakeAccount(ulong nonce, ulong balance, byte[] storageRoot = null)
            => new Account
            {
                Nonce = (EvmUInt256)nonce,
                Balance = (EvmUInt256)balance,
                StateRoot = storageRoot ?? DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };

        private static byte[] StorageKey(byte[] owner, byte[] slot)
        {
            var k = new byte[64];
            Buffer.BlockCopy(owner, 0, k, 0, 32);
            Buffer.BlockCopy(slot, 0, k, 32, 32);
            return k;
        }

        private Account ReadFlatAccount(byte[] accountHash)
        {
            var raw = _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash);
            if (raw == null) return null;
            var encoded = new byte[raw.Length - 20];
            Buffer.BlockCopy(raw, 20, encoded, 0, encoded.Length);
            return _enc.Decode(encoded);
        }

        [Fact]
        public async System.Threading.Tasks.Task Reconcile_DeletesGhosts_AddsMissing_PatchesMismatches_AndIsIdempotent()
        {
            var contractC = Hash32(0x40);
            var eoaB = Hash32(0x60);
            var ghostG = Hash32(0x70);
            var missingM = Hash32(0x90);
            var patchedP = Hash32(0xB0);
            var ghostZ = Hash32(0xD0);

            var slot1 = Hash32(0x11);
            var slot2 = Hash32(0x22);
            var slot3 = Hash32(0x33);

            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            storageTrie.Put(slot1, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 }));
            storageTrie.Put(slot2, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x0A }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contractC, _enc.Encode(MakeAccount(1, 100, storageRoot)));
            stateTrie.Put(eoaB, _enc.Encode(MakeAccount(2, 200)));
            stateTrie.Put(missingM, _enc.Encode(MakeAccount(3, 300)));
            stateTrie.Put(patchedP, _enc.Encode(MakeAccount(4, 400)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            await _flat.SaveAccountByHashAsync(contractC, MakeAccount(1, 100, storageRoot));
            await _flat.SaveStorageByHashAsync(contractC, slot1, new byte[] { 0x09 });
            await _flat.SaveStorageByHashAsync(contractC, slot2, new byte[] { 0x77 });
            await _flat.SaveStorageByHashAsync(contractC, slot3, new byte[] { 0x55 });
            await _flat.SaveAccountByHashAsync(eoaB, MakeAccount(2, 200));
            await _flat.SaveStorageByHashAsync(eoaB, slot1, new byte[] { 0x66 });
            await _flat.SaveAccountByHashAsync(ghostG, MakeAccount(9, 900));
            await _flat.SaveStorageByHashAsync(ghostG, slot1, new byte[] { 0x44 });
            await _flat.SaveAccountByHashAsync(patchedP, MakeAccount(4, 999));
            await _flat.SaveAccountByHashAsync(ghostZ, MakeAccount(8, 800));

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);
            var result = await reconciler.ReconcileFlatStateAsync(stateRoot, null, CancellationToken.None);

            Assert.Equal(4, result.AccountsScanned);
            Assert.Equal(2, result.SlotsScanned);
            Assert.Equal(2, result.GhostAccountsDeleted);
            Assert.Equal(3, result.GhostSlotsDeleted);
            Assert.Equal(1, result.AccountsAdded);
            Assert.Equal(0, result.SlotsAdded);
            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.SlotsPatched);

            Assert.Null(ReadFlatAccount(ghostG));
            Assert.Null(ReadFlatAccount(ghostZ));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contractC, slot3)));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(eoaB, slot1)));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(ghostG, slot1)));
            Assert.Equal(new byte[] { 0x09 }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contractC, slot1)));
            Assert.Equal(new byte[] { 0x0A }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contractC, slot2)));
            Assert.Equal((EvmUInt256)300, ReadFlatAccount(missingM).Balance);
            Assert.Equal((EvmUInt256)400, ReadFlatAccount(patchedP).Balance);
            Assert.Equal((EvmUInt256)100, ReadFlatAccount(contractC).Balance);
            Assert.Equal((EvmUInt256)200, ReadFlatAccount(eoaB).Balance);

            var second = await reconciler.ReconcileFlatStateAsync(stateRoot, null, CancellationToken.None);
            Assert.Equal(0, second.TotalRepairs);
            Assert.Equal(0, second.AccountsScanned);
            Assert.Equal(0, second.SlotsScanned);

            var verified = await reconciler.VerifyFlatStateAsync(stateRoot, null, CancellationToken.None);
            Assert.Equal(0, verified.TotalRepairs);
            Assert.Equal(4, verified.AccountsScanned);
            Assert.Equal(2, verified.SlotsScanned);
        }

        [Fact]
        public async System.Threading.Tasks.Task Verify_ReportsDiffs_ButWritesNothing()
        {
            var trieStore = new InMemoryContentNodeStore();
            var contract = Hash32(0x42);
            var missing = Hash32(0x66);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(1, 50)));
            stateTrie.Put(missing, _enc.Encode(MakeAccount(2, 60)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var ghost = Hash32(0x90);
            await _flat.SaveAccountByHashAsync(contract, MakeAccount(1, 999));
            await _flat.SaveAccountByHashAsync(ghost, MakeAccount(9, 900));
            await _flat.SaveStorageByHashAsync(ghost, Hash32(0x11), new byte[] { 0x01 });

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);
            var report = await reconciler.VerifyFlatStateAsync(stateRoot, null, CancellationToken.None);

            Assert.Equal(1, report.GhostAccountsDeleted);
            Assert.Equal(1, report.GhostSlotsDeleted);
            Assert.Equal(1, report.AccountsAdded);
            Assert.Equal(1, report.AccountsPatched);

            Assert.NotNull(ReadFlatAccount(ghost));
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(ghost, Hash32(0x11))));
            Assert.Equal((EvmUInt256)999, ReadFlatAccount(contract).Balance);
            Assert.Null(ReadFlatAccount(missing));

            var repaired = await reconciler.ReconcileFlatStateAsync(stateRoot, null, CancellationToken.None);
            Assert.Equal(report.TotalRepairs, repaired.TotalRepairs);
            var clean = await reconciler.VerifyFlatStateAsync(stateRoot, null, CancellationToken.None);
            Assert.Equal(0, clean.TotalRepairs);
        }

        [Fact]
        public async System.Threading.Tasks.Task Reconcile_EmptyFlat_PopulatesEverythingFromTrie()
        {
            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            var slot = Hash32(0x21);
            storageTrie.Put(slot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x05 }));
            storageTrie.SaveNodesToStorage();

            var contract = Hash32(0x42);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(1, 50, storageTrie.Root.GetHash())));
            stateTrie.SaveNodesToStorage();

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);
            var result = await reconciler.ReconcileFlatStateAsync(stateTrie.Root.GetHash(), null, CancellationToken.None);

            Assert.Equal(1, result.AccountsAdded);
            Assert.Equal(1, result.SlotsAdded);
            Assert.Equal(0, result.GhostAccountsDeleted + result.GhostSlotsDeleted);
            Assert.Equal((EvmUInt256)50, ReadFlatAccount(contract).Balance);
            Assert.Equal(new byte[] { 0x05 }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contract, slot)));
        }

        private sealed class LossyNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            private readonly byte[] _lostHash;
            private readonly bool _returnEmpty;
            public LossyNodeStore(ITrieNodeStore inner, byte[] lostHash, bool returnEmpty = false)
            {
                _inner = inner; _lostHash = lostHash; _returnEmpty = returnEmpty;
            }
            public byte[] Get(Nethereum.Merkle.Patricia.Nodes.Node reference)
            {
                if (reference is Nethereum.Merkle.Patricia.Nodes.HashNode h && h.Hash != null
                    && new System.ReadOnlySpan<byte>(h.Hash).SequenceEqual(_lostHash))
                    return _returnEmpty ? Array.Empty<byte>() : null;
                return _inner.Get(reference);
            }
            public bool Contains(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        [Fact]
        public async System.Threading.Tasks.Task Reconcile_UnresolvableTrieNode_ThrowsInsteadOfDeleting()
        {
            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            var slot = Hash32(0x21);
            storageTrie.Put(slot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x05 }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var contract = Hash32(0x42);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(1, 50, storageRoot)));
            stateTrie.SaveNodesToStorage();

            await _flat.SaveAccountByHashAsync(contract, MakeAccount(1, 50, storageRoot));
            await _flat.SaveStorageByHashAsync(contract, slot, new byte[] { 0x05 });

            var lossy = new LossyNodeStore(trieStore, storageRoot);
            var reconciler = new FlatStateReconciler(_mgr, _flat, lossy);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => reconciler.ReconcileFlatStateAsync(stateTrie.Root.GetHash(), null, CancellationToken.None));
            var damaged = Assert.Single(thrown.Subtrees);
            Assert.Equal(contract, damaged.AccountHash);
            Assert.Equal(storageRoot, damaged.StorageRoot);

            Assert.NotNull(ReadFlatAccount(contract));
            Assert.Equal(new byte[] { 0x05 }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contract, slot)));

            var lossyEmpty = new LossyNodeStore(trieStore, storageRoot, returnEmpty: true);
            var reconciler2 = new FlatStateReconciler(_mgr, _flat, lossyEmpty);
            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => reconciler2.ReconcileFlatStateAsync(stateTrie.Root.GetHash(), null, CancellationToken.None));
            Assert.NotNull(ReadFlatAccount(contract));
            Assert.Equal(new byte[] { 0x05 }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contract, slot)));
        }

        [Fact]
        public async System.Threading.Tasks.Task Reconcile_UnresolvableAccountTrieRoot_ReportsWholeTrieSentinel_InsteadOfCrashingUncaught()
        {
            var trieStore = new InMemoryContentNodeStore();
            var contract = Hash32(0x42);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(1, 50)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            await _flat.SaveAccountByHashAsync(contract, MakeAccount(1, 50));

            var lossy = new LossyNodeStore(trieStore, stateRoot);
            var reconciler = new FlatStateReconciler(_mgr, _flat, lossy);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => reconciler.ReconcileFlatStateAsync(stateRoot, null, CancellationToken.None));
            Assert.All(thrown.Subtrees, s =>
                Assert.Equal(FlatReconcileUnresolvableNodeException.WholeAccountTrieSentinel, s.AccountHash, ByteArrayComparer.Current));

            Assert.NotNull(ReadFlatAccount(contract));
        }

        [Fact]
        public async System.Threading.Tasks.Task Reconcile_ContractClearedToEmptyRoot_DropsAllItsFlatSlots()
        {
            var trieStore = new InMemoryContentNodeStore();
            var contract = Hash32(0x42);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(7, 70)));
            stateTrie.SaveNodesToStorage();

            await _flat.SaveAccountByHashAsync(contract, MakeAccount(7, 70));
            await _flat.SaveStorageByHashAsync(contract, Hash32(0x11), new byte[] { 0x01 });
            await _flat.SaveStorageByHashAsync(contract, Hash32(0x22), new byte[] { 0x02 });

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);
            var result = await reconciler.ReconcileFlatStateAsync(stateTrie.Root.GetHash(), null, CancellationToken.None);

            Assert.Equal(2, result.GhostSlotsDeleted);
            Assert.Equal(0, result.GhostAccountsDeleted);
            Assert.Equal(0, result.AccountsPatched);
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contract, Hash32(0x11))));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(contract, Hash32(0x22))));
            Assert.NotNull(ReadFlatAccount(contract));
        }

        [Fact]
        public async System.Threading.Tasks.Task Verify_SampleCap_ScansOnlyCap_CatchesPatchButDoesNotInflateGhosts()
        {
            var accountA = ShardKey(0x50, 0x01);
            var accountB = ShardKey(0x50, 0x02);
            var accountC = ShardKey(0x50, 0x03);
            var ghostG = ShardKey(0x50, 0x04);

            var slot = Hash32(0x11);

            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            storageTrie.Put(slot, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(accountA, _enc.Encode(MakeAccount(1, 100, storageRoot)));
            stateTrie.Put(accountB, _enc.Encode(MakeAccount(2, 200)));
            stateTrie.Put(accountC, _enc.Encode(MakeAccount(3, 300)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var paddedSlotValue = new byte[32];
            paddedSlotValue[31] = 0x09;
            await _flat.SaveAccountByHashAsync(accountA, MakeAccount(1, 100, storageRoot));
            _mgr.Put(RocksDbManager.CF_STATE_STORAGE, StorageKey(accountA, slot), paddedSlotValue);
            await _flat.SaveAccountByHashAsync(accountB, MakeAccount(2, 200));
            await _flat.SaveAccountByHashAsync(accountC, MakeAccount(3, 300));

            await _flat.SaveAccountByHashAsync(ghostG, MakeAccount(9, 900));
            await _flat.SaveStorageByHashAsync(ghostG, slot, new byte[] { 0x01 });

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);

            var sampled = await reconciler.VerifyFlatStateAsync(
                stateRoot, null, CancellationToken.None, sampleAccountsPerShard: 2);

            Assert.Equal(3, sampled.AccountsScanned);
            Assert.Equal(1, sampled.SlotsScanned);

            Assert.Equal(1, sampled.SlotsPatched);

            Assert.Equal(0, sampled.GhostAccountsDeleted);
            Assert.Equal(0, sampled.GhostSlotsDeleted);
            Assert.Equal(0, sampled.AccountsAdded);
            Assert.Equal(0, sampled.AccountsPatched);
            Assert.Equal(1, sampled.TotalRepairs);

            var full = await reconciler.VerifyFlatStateAsync(stateRoot, null, CancellationToken.None);
            Assert.Equal(3, full.AccountsScanned);
            Assert.Equal(1, full.SlotsScanned);
            Assert.Equal(1, full.SlotsPatched);
            Assert.Equal(1, full.GhostAccountsDeleted);
            Assert.Equal(1, full.GhostSlotsDeleted);
            Assert.Equal(0, full.AccountsAdded);
            Assert.Equal(0, full.AccountsPatched);
            Assert.Equal(3, full.TotalRepairs);

            Assert.NotNull(ReadFlatAccount(ghostG));
            Assert.Equal(paddedSlotValue, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(accountA, slot)));
        }

        [Fact]
        public async System.Threading.Tasks.Task Verify_ExecutionWrittenLeadingZeroSlot_FlatMatchesTrie_ZeroDiffs()
        {
            const string address = "0x00000000000000000000000000000000000042";
            var slot = new System.Numerics.BigInteger(9);

            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            var slotKey = StateKeys.StorageSlotKey(slot);
            storageTrie.Put(slotKey, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x07 }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountKey = StateKeys.AccountKey(address);
            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(accountKey, _enc.Encode(MakeAccount(1, 100, storageRoot)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            await _flat.SaveAccountAsync(address, MakeAccount(1, 100, storageRoot));
            var paddedSlotValue = new byte[32];
            paddedSlotValue[31] = 0x07;
            await _flat.SaveStorageAsync(address, slot, paddedSlotValue);

            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore);
            var verified = await reconciler.VerifyFlatStateAsync(stateRoot, null, CancellationToken.None);

            Assert.Equal(1, verified.AccountsScanned);
            Assert.Equal(1, verified.SlotsScanned);
            Assert.Equal(0, verified.SlotsPatched);
            Assert.Equal(0, verified.AccountsPatched);
            Assert.Equal(0, verified.GhostAccountsDeleted);
            Assert.Equal(0, verified.GhostSlotsDeleted);
            Assert.Equal(0, verified.AccountsAdded);
            Assert.Equal(0, verified.SlotsAdded);
            Assert.Equal(0, verified.TotalRepairs);

            Assert.Equal(new byte[] { 0x07 }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(accountKey, slotKey)));
        }

        private sealed class AutoAdvancingTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            private readonly TimeSpan _step;

            public AutoAdvancingTimeProvider(DateTimeOffset start, TimeSpan step)
            {
                _now = start;
                _step = step;
            }

            public override DateTimeOffset GetUtcNow()
            {
                var current = _now;
                _now += _step;
                return current;
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task Given_AShardWalkingSlotsSlowerThanTheCountCadence_When_TheProgressIntervalElapses_Then_AProgressLineIsEmittedWithoutWaitingForTenMillionSlots()
        {
            var contract = ShardKey(0x00, 0x01);

            var trieStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(trieStore);
            storageTrie.Put(Hash32(0x11), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x01 }));
            storageTrie.Put(Hash32(0x22), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x02 }));
            storageTrie.Put(Hash32(0x33), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x03 }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var stateTrie = new PatriciaTrie(trieStore);
            stateTrie.Put(contract, _enc.Encode(MakeAccount(1, 100, storageRoot)));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var clock = new AutoAdvancingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(61));
            var reconciler = new FlatStateReconciler(_mgr, _flat, trieStore, clock);

            var progressLines = new System.Collections.Generic.List<string>();
            await reconciler.ReconcileFlatStateAsync(stateRoot, progressLines.Add, CancellationToken.None);

            Assert.NotEmpty(progressLines);
            Assert.Contains(progressLines, l => l.Contains("shard=") && l.Contains("slots="));
        }
    }
}
