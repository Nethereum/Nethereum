using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class WindowDirtyLayersFlushWiringTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();

        public WindowDirtyLayersFlushWiringTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-windowlayers-wiring-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static RocksDbStorageOptions PathKeyedOptions(string dataDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 100_000,
            TrieNodeHistoryIndex = true,
        };

        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }
        private static byte[] K(int block, int i) => _keccak.CalculateHash(new byte[] { (byte)block, (byte)i, 0x5A });
        private static byte[] V(int block, int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i, (byte)i, (byte)block, (byte)block, (byte)i, (byte)block, (byte)i, (byte)block });

        private static int CountNodeHistoryRows(RocksDbManager manager)
        {
            using var it = manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private static void RunArmedBlock(RocksDbChainStoreBundle bundle, IAtomicBlockFlush flush, ulong block)
        {
            bundle.NodeCommitBlockSource.Arm(block);
            var trie = new PatriciaTrie(bundle.StateTrieNodes, _hp) { Tracer = new TrieTracer() };
            for (int i = 0; i < 10; i++)
                trie.Put(K((int)block, i), V((int)block, i));
            trie.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();

            flush.FlushBlockAsync(flat: null, block, Hash32((byte)block)).GetAwaiter().GetResult();
            flush.DrainAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public void FlushBlockAsync_PathKeyedNodeHistory_PushesOneWindowLayerPerBlock_AndDropsItAtK1()
        {
            using var mgr = new RocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            Assert.NotNull(bundle.WindowLayers);
            Assert.IsType<CapturingJournalingPathNodeStore>(bundle.StateTrieNodes);
            Assert.Equal(0, bundle.WindowLayers.PushCount);

            RunArmedBlock(bundle, flush, block: 1);
            Assert.Equal(1, bundle.WindowLayers.PushCount);
            Assert.Equal(0, bundle.WindowLayers.LayerCount);

            RunArmedBlock(bundle, flush, block: 2);
            Assert.Equal(2, bundle.WindowLayers.PushCount);
            Assert.Equal(0, bundle.WindowLayers.LayerCount);

            RunArmedBlock(bundle, flush, block: 3);
            Assert.Equal(3, bundle.WindowLayers.PushCount);
            Assert.Equal(0, bundle.WindowLayers.LayerCount);
        }

        [Fact]
        public void FlushBlockAsync_PathKeyedNodeHistory_ConsultsTryGetPreImageExactlyOncePerJournaledNode()
        {
            using var mgr = new RocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            Assert.Equal(0, bundle.WindowLayers.LookupCount);
            Assert.Equal(0, CountNodeHistoryRows(mgr));

            RunArmedBlock(bundle, flush, block: 1);
            var rowsAfterBlock1 = CountNodeHistoryRows(mgr);
            Assert.True(rowsAfterBlock1 > 0, "expected block 1's Put workload to journal at least one keyed (>=32B) node");
            Assert.Equal(rowsAfterBlock1, bundle.WindowLayers.LookupCount);

            RunArmedBlock(bundle, flush, block: 2);
            var rowsAfterBlock2 = CountNodeHistoryRows(mgr);
            var block2Rows = rowsAfterBlock2 - rowsAfterBlock1;
            Assert.True(block2Rows > 0, "expected block 2's Put workload to journal at least one keyed (>=32B) node");
            Assert.Equal(rowsAfterBlock2, bundle.WindowLayers.LookupCount);

            Assert.True(bundle.WindowLayers.LookupCount > rowsAfterBlock1);
        }

        private static byte[] BlockKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            var pathLen = path?.Length ?? 0;
            var ownerLen = acct ? 0 : owner.Length;
            var key = new byte[8 + 1 + ownerLen + pathLen];
            var blockBe = RocksDbManager.Write64BE(block);
            Buffer.BlockCopy(blockBe, 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, key, o, ownerLen); o += ownerLen; }
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, o, pathLen);
            return key;
        }

        [Fact]
        public void FlushBlockAsync_K2Window_SameKeyTouchedTwice_JournalsEachBlocksOwnPreImage_FromWindowLayerNotDiskNotOwnWrite()
        {
            using var mgr = new RocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var owner = _keccak.CalculateHash(new byte[] { 0x77 });
            var path = new byte[] { 0x01, 0x02 };

            static byte[] BigValue(byte tag)
            {
                var v = new byte[40];
                for (int i = 0; i < v.Length; i++) v[i] = tag;
                return v;
            }

            var nodeN = new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 5, 6, 7, 8 }, Value = BigValue(0xAA) };
            var nodeN1 = new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 5, 6, 7, 8 }, Value = BigValue(0xBB) };
            Assert.True(nodeN.GetEncodedData().Length >= 32, "test node must be keyed (>=32B RLP) to be journaled at all");

            bundle.NodeCommitBlockSource.Arm(5);
            var setN = new TrieNodeSet();
            setN.Add(nodeN);
            bundle.StateTrieNodes.Commit(setN);
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(6);
            var setN1 = new TrieNodeSet();
            setN1.Add(nodeN1);
            bundle.StateTrieNodes.Commit(setN1);
            bundle.NodeCommitBlockSource.Clear();

            flush.FlushBlockAsync(flat: null, block: 6, Hash32(6)).GetAwaiter().GetResult();
            flush.DrainAsync().GetAwaiter().GetResult();

            var recordBlock5 = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(5, owner, path));
            var recordBlock6 = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(6, owner, path));

            Assert.NotNull(recordBlock5);
            Assert.Empty(recordBlock5);

            Assert.NotNull(recordBlock6);
            Assert.Equal(nodeN.GetEncodedData(), recordBlock6);
            Assert.NotEqual(nodeN1.GetEncodedData(), recordBlock6);
        }
    }
}
