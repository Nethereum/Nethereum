using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class NodeHistoryComposeTests : IDisposable
    {
        private readonly string _dir;
        private RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public NodeHistoryComposeTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-compose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        private RocksDbChainStoreBundle Open(RocksDbStorageOptions opts)
        {
            opts.DatabasePath = _dir;
            _mgr = new RocksDbManager(opts);
            return RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
        }

        [Fact]
        public void Default_HashMode_StateTrieNodes_Is_Hash_Store_And_No_Context()
        {
            using var bundle = Open(new RocksDbStorageOptions());
            Assert.Same(bundle.TrieNodes, bundle.StateTrieNodes);
            Assert.IsType<RocksDbTrieNodeStore>(bundle.StateTrieNodes);
            Assert.Null(bundle.NodeCommitBlockSource);
        }

        [Fact]
        public async Task PathKeyed_NoHistory_StateTrieNodes_Is_Bare_Path_Store_And_No_Context()
        {
            using var bundle = Open(new RocksDbStorageOptions { PathKeyedState = true, TrieNodeHistoryBlocks = -1 });
            Assert.IsType<RocksDbPathTrieNodeStore>(bundle.StateTrieNodes);
            Assert.False(ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            Assert.Null(bundle.NodeCommitBlockSource);

            await CommitOneArmedOrNot(bundle, armBlock: null);
            Assert.Equal(0, CountNodeHistory());
        }

        [Fact]
        public async Task PathKeyed_WithHistory_StateTrieNodes_Is_Journaling_And_Exposes_Context()
        {
            using var bundle = Open(new RocksDbStorageOptions { PathKeyedState = true, TrieNodeHistoryBlocks = 128 });
            Assert.IsType<CapturingJournalingPathNodeStore>(bundle.StateTrieNodes);
            Assert.False(ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            Assert.NotNull(bundle.NodeCommitBlockSource);

            await CommitOneArmedOrNot(bundle, armBlock: 9, drain: false);
            Assert.Equal(0, CountNodeHistory());

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 9, new byte[32]);
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            Assert.True(CountNodeHistory() > 0);
        }

        private static async Task CommitOneArmedOrNot(RocksDbChainStoreBundle bundle, ulong? armBlock, bool drain = true)
        {
            var trie = new PatriciaTrie(bundle.StateTrieNodes, _hp) { Tracer = new TrieTracer() };
            if (armBlock.HasValue) bundle.NodeCommitBlockSource.Arm(armBlock.Value);
            for (int i = 0; i < 6; i++)
                trie.Put(_keccak.CalculateHash(new byte[] { (byte)i, 0x11 }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            trie.SaveDirtyNodesToStorage();
            if (armBlock.HasValue)
            {
                bundle.NodeCommitBlockSource.Clear();
                if (drain)
                    await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, armBlock.Value, new byte[32]);
                    await ((IAtomicBlockFlush)bundle).DrainAsync();
            }
        }

        private int CountNodeHistory()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        public void Dispose()
        {
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
