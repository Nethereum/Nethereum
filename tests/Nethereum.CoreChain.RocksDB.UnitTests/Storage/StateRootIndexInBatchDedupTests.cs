using System;
using System.IO;
using System.Threading.Tasks;
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
    public class StateRootIndexInBatchDedupTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();

        public StateRootIndexInBatchDedupTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-rootidx-dedup-" + Guid.NewGuid().ToString("N"));
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

        private sealed class CapturingStore : ITrieNodeStore
        {
            private readonly RocksDbPathTrieNodeStore _inner;
            public TrieNodeSet LastCommitted;
            public CapturingStore(RocksDbPathTrieNodeStore inner) => _inner = inner;
            public void Commit(TrieNodeSet nodes) { LastCommitted = nodes; _inner.Commit(nodes); }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        [Fact]
        public async Task TwoBlocksSameRootInOneWindow_RootIndexMapsToEarlierBlock()
        {
            var scratchDir = Path.Combine(_dir, "scratch");
            Directory.CreateDirectory(scratchDir);
            using var scratchMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = scratchDir, PathKeyedState = true });
            var scratch = new CapturingStore(new RocksDbPathTrieNodeStore(scratchMgr));
            var trie = new PatriciaTrie(scratch, _hp) { Tracer = new TrieTracer() };
            trie.Put(_keccak.CalculateHash(new byte[] { 0x01 }), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x2A }));
            trie.SaveDirtyNodesToStorage();
            var sharedRootHash = trie.Root.GetHash();
            var sharedSet = scratch.LastCommitted;
            Assert.NotNull(sharedSet);
            Assert.Contains(sharedSet.Nodes, n => (n.Owner == null || n.Owner.Length == 0) && (n.Path == null || n.Path.Length == 0));

            using var mgr = new RocksDbManager(PathKeyedOptions(_dir));
            using var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            bundle.NodeCommitBlockSource.Arm(10);
            bundle.StateTrieNodes.Commit(sharedSet);
            bundle.NodeCommitBlockSource.Arm(11);
            bundle.StateTrieNodes.Commit(sharedSet);
            bundle.NodeCommitBlockSource.Clear();

            await flush.FlushBlockAsync(flat: null, block: 11, hash: Hash32(11));
            await flush.DrainAsync();

            var index = new RocksDbNodeReverseDiffStore(mgr, buildKeyMajorIndex: true);
            var owner = index.FindBlockByStateRoot(sharedRootHash);
            Assert.Equal(10UL, owner);
        }
    }
}
