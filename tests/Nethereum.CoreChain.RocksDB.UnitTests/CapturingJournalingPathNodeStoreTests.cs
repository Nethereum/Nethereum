using System;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CapturingJournalingPathNodeStoreTests : IDisposable
    {
        private readonly string _dir;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public CapturingJournalingPathNodeStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-cjpns-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x22 });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });

        private RocksDbManager NewManager(string name)
        {
            var path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            return new RocksDbManager(new RocksDbStorageOptions { DatabasePath = path, PathKeyedState = true });
        }

        private static (CapturingJournalingPathNodeStore decorator, RocksDbPathTrieNodeStore inner, RocksDbNodeReverseDiffStore journal, NodeCommitBlockContext ctx)
            Build(RocksDbManager mgr, int nodeHistoryBlocks = 0, int pruneInterval = 1000)
        {
            var inner = new RocksDbPathTrieNodeStore(mgr);
            var journal = new RocksDbNodeReverseDiffStore(mgr, buildKeyMajorIndex: false);
            var ctx = new NodeCommitBlockContext();
            var floor = new FixedWindowFloorPolicy(nodeHistoryBlocks);
            var journaling = new JournalingPathNodeStore(inner, journal, ctx, floor, pruneInterval);
            var decorator = new CapturingJournalingPathNodeStore(journaling, ctx);
            return (decorator, inner, journal, ctx);
        }

        [Fact]
        public void Armed_Commit_Is_Deferred_Not_Yet_Persisted_Until_Drained()
        {
            using var mgr = NewManager("defer");
            var (decorator, inner, journal, ctx) = Build(mgr);
            var trie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(7);
            for (int i = 0; i < 6; i++) trie.Put(K(i), V(i + 1));
            trie.SaveDirtyNodesToStorage();
            ctx.Clear();

            Assert.False(inner.ContainsKey(trie.Root.GetHash()));
            Assert.Equal(0, CountNodeHistoryAt(mgr, 7));

            var captured = decorator.TakeCaptured();
            Assert.True(captured.Count > 0);
            journal.RecordAndCommit(7, inner, captured);

            var loaded = PatriciaTrie.LoadFromStorage(trie.Root.GetHash(), inner);
            Assert.Equal(V(1), loaded.Get(K(0)));
            Assert.True(CountNodeHistoryAt(mgr, 7) > 0);
        }

        [Fact]
        public void Unarmed_Commit_Passes_Straight_Through_No_Coalesce()
        {
            using var mgr = NewManager("unarmed");
            var (decorator, inner, _, ctx) = Build(mgr);
            var trie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };

            for (int i = 0; i < 6; i++) trie.Put(K(i), V(i + 1));
            trie.SaveDirtyNodesToStorage();

            var loaded = PatriciaTrie.LoadFromStorage(trie.Root.GetHash(), inner);
            Assert.Equal(V(1), loaded.Get(K(0)));

            var captured = decorator.TakeCaptured();
            Assert.Equal(0, captured.Count);
            Assert.Empty(captured.Deletes);
        }

        [Fact]
        public void Coalesces_Multiple_Commits_In_Same_Block_Into_One_Captured_Set()
        {
            using var mgr = NewManager("coalesce");
            var (decorator, inner, _, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xAB });
            var account = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(1);
            for (int i = 0; i < 4; i++) account.Put(K(i), V(i + 1));
            account.SaveDirtyNodesToStorage();
            storage.Put(K(50), V(50));
            storage.SaveDirtyNodesToStorage();
            ctx.Clear();

            var captured = decorator.TakeCaptured();
            Assert.True(captured.Count >= 2, "expected nodes from both the account and storage commits");
            Assert.Contains(captured.Nodes, n => n.Owner == null || n.Owner.Length == 0);
            Assert.Contains(captured.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(owner));

            Assert.False(inner.ContainsKey(account.Root.GetHash()));
        }

        [Fact]
        public void CoalescedSingleApply_IsByteIdentical_ToOldMultiCommitPerBlock()
        {
            using var mgrOld = NewManager("old-multicommit");
            using var mgrNew = NewManager("new-coalesced");

            var owner = _keccak.CalculateHash(new byte[] { 0xCD });
            const ulong block = 3;

            var innerOld = new RocksDbPathTrieNodeStore(mgrOld);
            var journalOld = new RocksDbNodeReverseDiffStore(mgrOld, buildKeyMajorIndex: false);
            var ctxOld = new NodeCommitBlockContext();
            var storeOld = new JournalingPathNodeStore(innerOld, journalOld, ctxOld, new FixedWindowFloorPolicy(0), 1000);
            var accountOld = new PatriciaTrie(storeOld, _hp) { Tracer = new TrieTracer() };
            var storageOld = new PatriciaTrie(storeOld, owner, _hp) { Tracer = new TrieTracer() };

            ctxOld.Arm(block);
            for (int i = 0; i < 4; i++) accountOld.Put(K(i), V(i + 1));
            accountOld.SaveDirtyNodesToStorage();
            storageOld.Put(K(50), V(50));
            storageOld.SaveDirtyNodesToStorage();
            ctxOld.Clear();

            var innerNew = new RocksDbPathTrieNodeStore(mgrNew);
            var journalNew = new RocksDbNodeReverseDiffStore(mgrNew, buildKeyMajorIndex: false);
            var ctxNew = new NodeCommitBlockContext();
            var storeNew = new JournalingPathNodeStore(innerNew, journalNew, ctxNew, new FixedWindowFloorPolicy(0), 1000);
            var decoratorNew = new CapturingJournalingPathNodeStore(storeNew, ctxNew);
            var accountNew = new PatriciaTrie(decoratorNew, _hp) { Tracer = new TrieTracer() };
            var storageNew = new PatriciaTrie(decoratorNew, owner, _hp) { Tracer = new TrieTracer() };

            ctxNew.Arm(block);
            for (int i = 0; i < 4; i++) accountNew.Put(K(i), V(i + 1));
            accountNew.SaveDirtyNodesToStorage();
            storageNew.Put(K(50), V(50));
            storageNew.SaveDirtyNodesToStorage();
            ctxNew.Clear();
            var captured = decoratorNew.TakeCaptured();
            journalNew.RecordAndCommit(block, innerNew, captured);

            Assert.Equal(accountOld.Root.GetHash(), accountNew.Root.GetHash());
            Assert.Equal(storageOld.Root.GetHash(), storageNew.Root.GetHash());

            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_STATE_TRIE_STORAGE);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_NODE_HISTORY);

            Assert.True(DumpCf(mgrOld, RocksDbManager.CF_NODE_HISTORY).Count > 0);
        }

        [Fact]
        public void New_Armed_Block_Clears_The_Previous_Blocks_Buffer()
        {
            using var mgr = NewManager("newblock");
            var (decorator, _, _, ctx) = Build(mgr);
            var trie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(1);
            trie.Put(K(1), V(1));
            trie.SaveDirtyNodesToStorage();
            ctx.Clear();

            ctx.Arm(2);
            trie.Put(K(2), V(2));
            trie.SaveDirtyNodesToStorage();
            ctx.Clear();

            var captured = decorator.TakeCaptured();
            Assert.True(captured.Count > 0);
            var second = decorator.TakeCaptured();
            Assert.Equal(0, second.Count);
        }

        [Fact]
        public void ReCommit_Of_AlreadyDrained_Block_StartsFresh_DoesNotAccreteStaleEntries()
        {
            using var mgr = NewManager("retry-drained");
            var (decorator, _, _, ctx) = Build(mgr);
            var ownerA = _keccak.CalculateHash(new byte[] { 0x11 });
            var ownerB = _keccak.CalculateHash(new byte[] { 0x22 });
            var trieA = new PatriciaTrie(decorator, ownerA, _hp) { Tracer = new TrieTracer() };
            var trieB = new PatriciaTrie(decorator, ownerB, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(5);
            trieA.Put(K(1), V(1));
            trieA.SaveDirtyNodesToStorage();
            ctx.Clear();
            var firstDrain = decorator.TakeCaptured();
            Assert.True(firstDrain.Count > 0);
            Assert.All(firstDrain.Nodes, n => Assert.True(n.Owner != null && n.Owner.SequenceEqual(ownerA)));

            ctx.Arm(5);
            trieB.Put(K(2), V(2));
            trieB.SaveDirtyNodesToStorage();
            ctx.Clear();
            var secondDrain = decorator.TakeCaptured();

            Assert.True(secondDrain.Count > 0);
            Assert.All(secondDrain.Nodes, n => Assert.True(n.Owner != null && n.Owner.SequenceEqual(ownerB)));
            Assert.DoesNotContain(secondDrain.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerA));
        }

        [Fact]
        public void RetriedSameBlock_WithoutIntervening_TakeCaptured_Coalesces_ProducerTheoretical()
        {
            using var mgr = NewManager("retry-undrained");
            var (decorator, _, _, ctx) = Build(mgr);
            var trieA = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };
            var owner = _keccak.CalculateHash(new byte[] { 0xEF });
            var trieB = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(9);
            trieA.Put(K(1), V(1));
            trieA.SaveDirtyNodesToStorage();
            ctx.Clear();

            ctx.Arm(9);
            trieB.Put(K(2), V(2));
            trieB.SaveDirtyNodesToStorage();
            ctx.Clear();

            var captured = decorator.TakeCaptured();
            Assert.Contains(captured.Nodes, n => n.Owner == null || n.Owner.Length == 0);
            Assert.Contains(captured.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(owner));
        }

        [Fact]
        public void ContainsKey_And_TryGetRawNode_And_ClearCache_Forward_To_Inner()
        {
            using var mgr = NewManager("forward");
            var (decorator, inner, _, _) = Build(mgr);
            var trie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };

            trie.Put(K(0), V(1));
            trie.SaveDirtyNodesToStorage();

            Assert.Equal(inner.ContainsKey(trie.Root.GetHash()), decorator.ContainsKey(trie.Root.GetHash()));
            Assert.True(decorator.ContainsKey(trie.Root.GetHash()));

            var raw = decorator.TryGetRawNode(Array.Empty<byte>(), Array.Empty<byte>());
            Assert.Equal(inner.TryGetRawNode(Array.Empty<byte>(), Array.Empty<byte>()), raw);

            decorator.ClearCache();
        }

        [Fact]
        public void ContainsKey_BufferedRootMismatch_FallsThroughToInner_ResolvesDurableRoot()
        {
            using var mgr = NewManager("containskey-fallthrough");
            var (decorator, inner, journal, ctx) = Build(mgr);

            var durableTrie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };
            durableTrie.Put(K(0), V(1));
            durableTrie.SaveDirtyNodesToStorage();
            var durableRoot = durableTrie.Root.GetHash();
            Assert.True(inner.ContainsKey(durableRoot), "test setup: block 1's root should already be durable");

            ctx.Arm(2);
            durableTrie.Put(K(1), V(2));
            durableTrie.SaveDirtyNodesToStorage();
            var bufferedRoot = durableTrie.Root.GetHash();
            Assert.NotEqual(Convert.ToHexString(durableRoot), Convert.ToHexString(bufferedRoot));

            Assert.True(decorator.ContainsKey(bufferedRoot));
            Assert.True(decorator.ContainsKey(durableRoot));
            Assert.False(decorator.ContainsKey(K(99)));

            ctx.Clear();
        }

        [Fact]
        public void DeleteRange_Unarmed_Forwards_Immediately_To_Inner_JournalingStore()
        {
            using var mgr = NewManager("deleterange-unarmed");
            var (decorator, inner, _, _) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xCD });
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            storage.Put(K(1), V(1));
            storage.SaveDirtyNodesToStorage();
            var beforeWipe = PatriciaTrie.LoadFromStorage(storage.Root.GetHash(), inner, owner);
            Assert.Equal(V(1), beforeWipe.Get(K(1)));

            decorator.DeleteRange(owner);

            var raw = inner.TryGetRawNode(owner, Array.Empty<byte>());
            Assert.Null(raw);
        }

        [Fact]
        public void DeleteRange_Armed_Is_Deferred_Not_Yet_Forwarded_Until_Drained()
        {
            using var mgr = NewManager("deleterange-armed-defer");
            var (decorator, inner, journal, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xCD });
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            storage.Put(K(1), V(1));
            storage.SaveDirtyNodesToStorage();
            var beforeWipe = PatriciaTrie.LoadFromStorage(storage.Root.GetHash(), inner, owner);
            Assert.Equal(V(1), beforeWipe.Get(K(1)));

            ctx.Arm(2);
            decorator.DeleteRange(owner);
            ctx.Clear();

            Assert.NotNull(inner.TryGetRawNode(owner, Array.Empty<byte>()));

            var wipes = decorator.TakeCapturedWipes();
            Assert.Single(wipes);
            Assert.Equal(2UL, wipes[0].Block);
            Assert.Equal(owner, wipes[0].Owner);

            journal.RecordAndDeleteRange(wipes[0].Block, inner, wipes[0].Owner);

            var raw = inner.TryGetRawNode(owner, Array.Empty<byte>());
            Assert.Null(raw);
        }

        [Fact]
        public void TakeCapturedWipes_EmptyWhenNoWipe_AndResetsAfterDrain()
        {
            using var mgr = NewManager("deleterange-empty");
            var (decorator, _, _, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xEE });

            Assert.Empty(decorator.TakeCapturedWipes());

            ctx.Arm(4);
            decorator.DeleteRange(owner);
            ctx.Clear();

            Assert.Single(decorator.TakeCapturedWipes());
            Assert.Empty(decorator.TakeCapturedWipes());
        }

        [Fact]
        public void DeleteRange_AtWindowOne_NeverPurgesSameBlockSameOwnerPuts_ResurrectionFixInert()
        {
            using var mgr = NewManager("resurrection-inert-k1");
            var (decorator, _, _, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xFA });
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(9);
            decorator.DeleteRange(owner);
            storage.Put(K(0), V(1));
            storage.SaveDirtyNodesToStorage();
            ctx.Clear();

            var captured = decorator.TakeCaptured();
            Assert.True(captured.Count > 0, "a same-block, same-owner put must survive the wipe at window=1");
            Assert.Contains(captured.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(owner));
        }

        [Fact]
        public void DeleteRange_AfterSameBlockPut_PutSurvives_PurgeOwnerBelowBoundaryIsStrictlyBelow()
        {
            using var mgr = NewManager("resurrection-put-then-wipe-k1");
            var (decorator, _, _, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xFB });
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(11);
            storage.Put(K(0), V(1));
            storage.SaveDirtyNodesToStorage();
            decorator.DeleteRange(owner);
            ctx.Clear();

            var captured = decorator.TakeCaptured();
            Assert.True(captured.Count > 0,
                "PurgeOwnerBelow's boundary is strictly-below: a put buffered in the SAME block as the wipe must survive");
            Assert.Contains(captured.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(owner));
        }

        [Fact]
        public void TakeCapturedJournal_AtWindowOne_ReturnsExactlyOneEntry_PerDrain()
        {
            using var mgr = NewManager("journal-k1");
            var (decorator, _, _, ctx) = Build(mgr);
            var trie = new PatriciaTrie(decorator, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(1);
            trie.Put(K(1), V(1));
            trie.SaveDirtyNodesToStorage();
            ctx.Clear();

            var journal = decorator.TakeCapturedJournal();
            var coalesced = decorator.TakeCaptured();

            Assert.Single(journal);
            Assert.Equal(1UL, journal[0].Block);
            Assert.Equal(coalesced.Count, journal[0].Set.Count);

            Assert.Empty(decorator.TakeCapturedJournal());
        }

        [Fact]
        public void TakeCapturedJournal_AcrossTwoUndrainedBlocks_ReturnsOneEntryPerBlock_InAscendingOrder()
        {
            using var mgr = NewManager("journal-k2");
            var (decorator, _, _, ctx) = Build(mgr);
            var ownerA = _keccak.CalculateHash(new byte[] { 0xA1 });
            var ownerB = _keccak.CalculateHash(new byte[] { 0xB2 });
            var trieA = new PatriciaTrie(decorator, ownerA, _hp) { Tracer = new TrieTracer() };
            var trieB = new PatriciaTrie(decorator, ownerB, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(5);
            trieA.Put(K(1), V(1));
            trieA.SaveDirtyNodesToStorage();
            ctx.Clear();

            ctx.Arm(6);
            trieB.Put(K(2), V(2));
            trieB.SaveDirtyNodesToStorage();
            ctx.Clear();

            var journal = decorator.TakeCapturedJournal();
            var coalesced = decorator.TakeCaptured();

            Assert.Equal(2, journal.Count);
            Assert.Equal(5UL, journal[0].Block);
            Assert.Equal(6UL, journal[1].Block);
            Assert.All(journal[0].Set.Nodes, n => Assert.True(n.Owner != null && n.Owner.SequenceEqual(ownerA)));
            Assert.All(journal[1].Set.Nodes, n => Assert.True(n.Owner != null && n.Owner.SequenceEqual(ownerB)));

            Assert.Contains(coalesced.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerA));
            Assert.Contains(coalesced.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerB));

            Assert.Empty(decorator.TakeCapturedJournal());
            Assert.Equal(0, decorator.TakeCaptured().Count);
        }

        [Fact]
        public void TakeCapturedJournal_SameKeyTouchedTwiceAcrossWindow_BothBlocksJournalTheirOwnValue()
        {
            using var mgr = NewManager("journal-samekey");
            var (decorator, _, _, ctx) = Build(mgr);
            var owner = _keccak.CalculateHash(new byte[] { 0xCC });
            var storage = new PatriciaTrie(decorator, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(10);
            storage.Put(K(1), V(1));
            storage.SaveDirtyNodesToStorage();
            var rootHashBlock10 = storage.Root.GetHash();
            ctx.Clear();

            ctx.Arm(11);
            storage.Put(K(1), V(2));
            storage.SaveDirtyNodesToStorage();
            var rootHashBlock11 = storage.Root.GetHash();
            ctx.Clear();

            var journal = decorator.TakeCapturedJournal();
            var coalesced = decorator.TakeCaptured();

            Assert.Equal(2, journal.Count);
            Assert.Equal(1, coalesced.Count);

            var node10 = Assert.Single(journal[0].Set.Nodes);
            var node11 = Assert.Single(journal[1].Set.Nodes);
            Assert.Equal(rootHashBlock10, _hp.ComputeHash(node10.GetEncodedData()));
            Assert.Equal(rootHashBlock11, _hp.ComputeHash(node11.GetEncodedData()));
            Assert.NotEqual(node10.GetEncodedData(), node11.GetEncodedData());

            var coalescedNode = Assert.Single(coalesced.Nodes);
            Assert.Equal(node11.GetEncodedData(), coalescedNode.GetEncodedData());
        }

        [Fact]
        public void DiscardCaptured_AfterAbandonedBlock_NextBlockDrainsOnlyItsOwnEntries()
        {
            using var mgr = NewManager("discard-abandoned");
            var (decorator, _, _, ctx) = Build(mgr);
            var ownerN = _keccak.CalculateHash(new byte[] { 0xD1 });
            var ownerM = _keccak.CalculateHash(new byte[] { 0xD2 });
            var trieN = new PatriciaTrie(decorator, ownerN, _hp) { Tracer = new TrieTracer() };
            var trieM = new PatriciaTrie(decorator, ownerM, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(100);
            trieN.Put(K(1), V(1));
            trieN.SaveDirtyNodesToStorage();
            ctx.Clear();

            decorator.DiscardCaptured();

            ctx.Arm(101);
            trieM.Put(K(2), V(2));
            trieM.SaveDirtyNodesToStorage();
            ctx.Clear();

            var journal = decorator.TakeCapturedJournal();
            var coalesced = decorator.TakeCaptured();

            Assert.Single(journal);
            Assert.Equal(101UL, journal[0].Block);
            Assert.DoesNotContain(journal[0].Set.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerN));
            Assert.All(journal[0].Set.Nodes, n => Assert.True(n.Owner != null && n.Owner.SequenceEqual(ownerM)));

            Assert.DoesNotContain(coalesced.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerN));
            Assert.Contains(coalesced.Nodes, n => n.Owner != null && n.Owner.SequenceEqual(ownerM));
        }

        [Fact]
        public void DiscardCaptured_WhenNothingBuffered_IsANoOp()
        {
            using var mgr = NewManager("discard-noop");
            var (decorator, _, _, _) = Build(mgr);

            decorator.DiscardCaptured();

            Assert.Empty(decorator.TakeCapturedJournal());
            Assert.Equal(0, decorator.TakeCaptured().Count);
        }

        private static int CountNodeHistoryAt(RocksDbManager mgr, ulong block)
        {
            var be = RocksDbManager.Write64BE(block);
            using var it = mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                bool match = k.Length >= 8;
                for (int i = 0; i < 8 && match; i++) if (k[i] != be[i]) match = false;
                if (match) n++;
                it.Next();
            }
            return n;
        }

        private static System.Collections.Generic.Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new System.Collections.Generic.Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private static void AssertCfIdentical(RocksDbManager a, RocksDbManager b, string cf)
        {
            var dumpA = DumpCf(a, cf);
            var dumpB = DumpCf(b, cf);
            Assert.True(dumpA.Count == dumpB.Count,
                $"[{cf}] row count differs: old-multicommit={dumpA.Count} new-coalesced={dumpB.Count}");
            foreach (var kv in dumpA)
            {
                Assert.True(dumpB.TryGetValue(kv.Key, out var bVal),
                    $"[{cf}] key {kv.Key} present in old-multicommit, missing in new-coalesced");
                Assert.Equal(kv.Value, bVal);
            }
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
