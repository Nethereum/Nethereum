using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class JournalingPathNodeStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public JournalingPathNodeStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-jpns-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x11 });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });

        private (JournalingPathNodeStore wrapper, RocksDbPathTrieNodeStore inner, RocksDbNodeReverseDiffStore journal, NodeCommitBlockContext ctx)
            Build(int nodeHistoryBlocks, int pruneInterval, bool buildIndex = false)
        {
            var inner = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: buildIndex);
            var ctx = new NodeCommitBlockContext();
            var floor = new FixedWindowFloorPolicy(nodeHistoryBlocks);
            var wrapper = new JournalingPathNodeStore(inner, journal, ctx, floor, pruneInterval);
            return (wrapper, inner, journal, ctx);
        }

        private sealed class RecordingStore : ITrieNodeStore
        {
            private readonly INodeCommitBlockSource _source;
            public readonly List<ulong?> Observed = new();
            public RecordingStore(INodeCommitBlockSource source) => _source = source;
            public void Commit(TrieNodeSet nodes) => Observed.Add(_source.CurrentBlock);
            public byte[] Get(Node r) => null;
            public bool Contains(Node r) => false;
            public bool ContainsKey(byte[] r) => false;
            public void Flush() { }
            public void Clear() { }
        }

        [Fact]
        public void SharedContext_Arm_Then_Clear_Is_Observed_At_Commit()
        {
            var ctx = new NodeCommitBlockContext();
            var store = new RecordingStore(ctx);

            ctx.Arm(7);
            store.Commit(new TrieNodeSet());
            ctx.Clear();
            store.Commit(new TrieNodeSet());

            Assert.Equal(new ulong?[] { 7UL, null }, store.Observed);
        }

        [Fact]
        public void Armed_Commit_Writes_NodeHistory_And_Advances_Inner()
        {
            var (wrapper, inner, _, ctx) = Build(nodeHistoryBlocks: 0, pruneInterval: 1000);
            var trie = new PatriciaTrie(wrapper, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(7);
            for (int i = 0; i < 6; i++) trie.Put(K(i), V(i + 1));
            trie.SaveDirtyNodesToStorage();
            ctx.Clear();

            Assert.True(CountNodeHistoryAt(7) > 0);
            var loaded = PatriciaTrie.LoadFromStorage(trie.Root.GetHash(), inner);
            Assert.Equal(V(1), loaded.Get(K(0)));
        }

        [Fact]
        public void Unarmed_Commit_Is_Plain_Inner_With_No_NodeHistory()
        {
            var (wrapper, inner, _, ctx) = Build(nodeHistoryBlocks: 0, pruneInterval: 1000);
            var trie = new PatriciaTrie(wrapper, _hp) { Tracer = new TrieTracer() };

            for (int i = 0; i < 6; i++) trie.Put(K(i), V(i + 1));
            trie.SaveDirtyNodesToStorage();

            Assert.Equal(0, CountNodeHistoryTotal());
            var loaded = PatriciaTrie.LoadFromStorage(trie.Root.GetHash(), inner);
            Assert.Equal(V(1), loaded.Get(K(0)));
        }

        [Fact]
        public void Prune_After_Interval_Drops_History_Below_Window_Floor()
        {
            var (wrapper, _, _, ctx) = Build(nodeHistoryBlocks: 2, pruneInterval: 1);
            var trie = new PatriciaTrie(wrapper, _hp) { Tracer = new TrieTracer() };

            for (ulong b = 1; b <= 5; b++)
            {
                ctx.Arm(b);
                for (int i = 0; i < 4; i++) trie.Put(K((int)b * 10 + i), V((int)b));
                trie.SaveDirtyNodesToStorage();
                ctx.Clear();
            }

            Assert.Equal(0, CountNodeHistoryAt(1));
            Assert.Equal(0, CountNodeHistoryAt(2));
            Assert.True(CountNodeHistoryAt(3) > 0);
            Assert.True(CountNodeHistoryAt(4) > 0);
            Assert.True(CountNodeHistoryAt(5) > 0);
        }

        [Fact]
        public void FullHistory_Window_Zero_Never_Prunes()
        {
            var (wrapper, _, _, ctx) = Build(nodeHistoryBlocks: 0, pruneInterval: 1);
            var trie = new PatriciaTrie(wrapper, _hp) { Tracer = new TrieTracer() };

            for (ulong b = 1; b <= 5; b++)
            {
                ctx.Arm(b);
                for (int i = 0; i < 4; i++) trie.Put(K((int)b * 10 + i), V((int)b));
                trie.SaveDirtyNodesToStorage();
                ctx.Clear();
            }

            for (ulong b = 1; b <= 5; b++)
                Assert.True(CountNodeHistoryAt(b) > 0, $"block {b} must be retained under full history");
        }

        [Fact]
        public void IndexOn_Writes_KeyMajor_Rows_And_FindBlobAsOf_Resolves()
        {
            var (wrapper, _, journal, ctx) = Build(nodeHistoryBlocks: 0, pruneInterval: 1000, buildIndex: true);
            var owner = _keccak.CalculateHash(new byte[] { 0xAB, 0xCD });
            var slot = _keccak.CalculateHash(new byte[] { 0x77 });
            var account = new PatriciaTrie(wrapper, _hp) { Tracer = new TrieTracer() };
            var storage = new PatriciaTrie(wrapper, owner, _hp) { Tracer = new TrieTracer() };

            ctx.Arm(1);
            for (int i = 0; i < 6; i++) account.Put(K(i), V(i + 1));
            storage.Put(slot, V(50));
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();
            ctx.Clear();

            ctx.Arm(3);
            storage.Put(slot, V(60));
            storage.SaveDirtyNodesToStorage();
            ctx.Clear();

            Assert.True(CountIndexRows() > 0);
            var b1Blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(3, owner, new byte[0]));
            Assert.Equal(b1Blob, journal.FindBlobAsOf(owner, new byte[0], 1));
        }

        private int CountNodeHistoryAt(ulong block)
        {
            var be = RocksDbManager.Write64BE(block);
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
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

        private int CountNodeHistoryTotal()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private int CountIndexRows()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private static byte[] HistoryKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            int ownerLen = acct ? 0 : owner.Length;
            int pathLen = path?.Length ?? 0;
            var key = new byte[8 + 1 + ownerLen + pathLen];
            Buffer.BlockCopy(RocksDbManager.Write64BE(block), 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, key, o, ownerLen); o += ownerLen; }
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, o, pathLen);
            return key;
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    public class FixedWindowFloorPolicyTests
    {
        [Theory]
        [InlineData(128, 1000UL, 872UL)]
        [InlineData(128, 100UL, 0UL)]
        [InlineData(1, 5UL, 4UL)]
        public void PositiveWindow_Returns_Head_Minus_N_Clamped(int n, ulong head, ulong expected)
        {
            Assert.Equal(expected, new FixedWindowFloorPolicy(n).FloorFor(head));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void NonPositiveWindow_Is_NeverPrune_Sentinel_Zero(int n)
        {
            var policy = new FixedWindowFloorPolicy(n);
            Assert.Equal(0UL, policy.FloorFor(1_000_000UL));
        }
    }
}
