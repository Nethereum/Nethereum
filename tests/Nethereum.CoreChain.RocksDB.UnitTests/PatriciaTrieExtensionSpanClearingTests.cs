using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PatriciaTrieExtensionSpanClearingTests : IDisposable
    {
        private static readonly byte[] SpannedBranch = { 0x3 };
        private static readonly byte[] SpannedDeeperBranch = { 0x3, 0xf };
        private static readonly byte[] ExtensionChild = { 0x3, 0xf, 0xf };

        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbPathTrieNodeStore _pathStore;

        public PatriciaTrieExtensionSpanClearingTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_extension_span_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath, PathKeyedState = true });
            _pathStore = new RocksDbPathTrieNodeStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
                try { Directory.Delete(_dbPath, true); } catch { }
        }

        [Fact]
        public void Given_AStoredBranchAtAPathInsideTheSpanOfAnExtensionThatAFreshTrieSaves_When_SavedWithExtensionSpanClearing_Then_TheStoredBranchIsDeletedInTheSameBatch()
        {
            SaveEarlierTrieWithBranchesInsideTheLaterSpan();
            var before = StoredBlobs(SpannedBranch, SpannedDeeperBranch);
            Assert.All(before, blob => Assert.NotNull(blob));
            var store = new CommitCapturingPathStore(_pathStore);

            var fresh = SaveFreshExtensionTrie(store, clearExtensionSpans: true);

            var commit = Assert.Single(store.Commits);
            Assert.Equal(new[] { SpannedBranch.ToHex(), SpannedDeeperBranch.ToHex() }, commit.Deletes.Select(d => d.Path.ToHex()));
            Assert.Equal(before.Select(b => b.ToHex()), commit.Deletes.Select(d => d.PrevBlob.ToHex()));
            Assert.Contains(commit.Nodes, n => n.Path.ToHex() == ExtensionChild.ToHex());
            Assert.All(StoredBlobs(SpannedBranch, SpannedDeeperBranch), blob => Assert.Null(blob));
            Assert.Equal(fresh.Root.GetEncodedData().ToHex(), Stored(Array.Empty<byte>()).ToHex());
            Assert.Equal(((ExtendedNode)fresh.Root).InnerNode.GetEncodedData().ToHex(), Stored(ExtensionChild).ToHex());
        }

        [Fact]
        public void Given_AnExtensionWhoseChildIsEmbedded_When_SavedWithExtensionSpanClearing_Then_NoInternalPathIsDeleted()
        {
            SaveEarlierTrieWithBranchesInsideTheLaterSpan();
            var before = StoredBlobs(SpannedBranch, SpannedDeeperBranch);
            var store = new CommitCapturingPathStore(_pathStore);
            var fresh = new PatriciaTrie(store) { ClearExtensionSpansOnSave = true };
            fresh.Put(new byte[] { 0x3f, 0xf1 }, new byte[] { 0x01 });
            fresh.Put(new byte[] { 0x3f, 0xf2 }, new byte[] { 0x02 });
            var extension = Assert.IsType<ExtendedNode>(fresh.Root);
            Assert.Equal(ExtensionChild.ToHex(), extension.Nibbles.ToHex());
            Assert.True(extension.InnerNode.GetEncodedData().Length < 32);

            fresh.SaveDirtyNodesToStorage();

            Assert.Empty(store.Commits.SelectMany(c => c.Deletes));
            Assert.Equal(before.Select(b => b.ToHex()), StoredBlobs(SpannedBranch, SpannedDeeperBranch).Select(b => b.ToHex()));
        }

        [Fact]
        public void Given_NoNodeAtAnyInternalPath_When_SavedWithExtensionSpanClearing_Then_TheCommittedSetHasNoDeletes()
        {
            Assert.All(StoredBlobs(SpannedBranch, SpannedDeeperBranch), blob => Assert.Null(blob));
            var store = new CommitCapturingPathStore(_pathStore);

            var fresh = SaveFreshExtensionTrie(store, clearExtensionSpans: true);

            Assert.IsType<ExtendedNode>(fresh.Root);
            var commit = Assert.Single(store.Commits);
            Assert.Empty(commit.Deletes);
            Assert.NotNull(Stored(ExtensionChild));
        }

        [Fact]
        public void Given_ExtensionSpanClearingIsOff_When_AFreshTrieSavesOverAStoredBranchInsideItsExtension_Then_TheStoredBranchSurvives()
        {
            SaveEarlierTrieWithBranchesInsideTheLaterSpan();
            var before = StoredBlobs(SpannedBranch, SpannedDeeperBranch);
            var store = new CommitCapturingPathStore(_pathStore);

            SaveFreshExtensionTrie(store, clearExtensionSpans: false);

            Assert.Empty(Assert.Single(store.Commits).Deletes);
            Assert.All(before, blob => Assert.NotNull(blob));
            Assert.Equal(before.Select(b => b.ToHex()), StoredBlobs(SpannedBranch, SpannedDeeperBranch).Select(b => b.ToHex()));
        }

        [Fact]
        public void Given_AHashKeyedStore_When_SavedWithExtensionSpanClearing_Then_NoDeleteIsIssued()
        {
            var hashKeyed = new InMemoryContentNodeStore();
            var earlier = new PatriciaTrie(hashKeyed);
            foreach (var key in EarlierKeys()) earlier.Put(key, Value);
            earlier.SaveDirtyNodesToStorage();
            var store = new CommitCapturingStore(hashKeyed);

            var fresh = SaveFreshExtensionTrie(store, clearExtensionSpans: true);

            Assert.IsType<ExtendedNode>(fresh.Root);
            Assert.Empty(Assert.Single(store.Commits).Deletes);
        }

        private static readonly byte[] Value = Enumerable.Repeat((byte)0x77, 40).ToArray();

        private static IEnumerable<byte[]> EarlierKeys() => new[]
        {
            Key(0x3, 0x0), Key(0x3, 0xf, 0x0), Key(0x3, 0xf, 0xf, 0x1), Key(0x3, 0xf, 0xf, 0x5), Key(0x3, 0xf, 0xf, 0x9), Key(0x7),
        };

        private void SaveEarlierTrieWithBranchesInsideTheLaterSpan()
        {
            var earlier = new PatriciaTrie(_pathStore);
            foreach (var key in EarlierKeys()) earlier.Put(key, Value);
            earlier.SaveDirtyNodesToStorage();
        }

        private static PatriciaTrie SaveFreshExtensionTrie(ITrieNodeStore store, bool clearExtensionSpans)
        {
            var fresh = new PatriciaTrie(store);
            if (clearExtensionSpans) fresh.ClearExtensionSpansOnSave = true;
            fresh.Put(Key(0x3, 0xf, 0xf, 0x5), Value);
            fresh.Put(Key(0x3, 0xf, 0xf, 0x9), Value);
            fresh.SaveDirtyNodesToStorage();
            return fresh;
        }

        private static byte[] Key(params byte[] leadingNibbles)
        {
            var key = new byte[32];
            for (var i = 0; i < 64; i++)
            {
                var nibble = i < leadingNibbles.Length ? leadingNibbles[i] : (byte)0xa;
                key[i / 2] |= (byte)(i % 2 == 0 ? nibble << 4 : nibble);
            }
            return key;
        }

        private byte[] Stored(byte[] path) => _pathStore.TryGetRawNode(Array.Empty<byte>(), path);

        private List<byte[]> StoredBlobs(params byte[][] paths) => paths.Select(Stored).ToList();

        private class CommitCapturingStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;

            public CommitCapturingStore(ITrieNodeStore inner) => _inner = inner;

            public List<TrieNodeSet> Commits { get; } = new();

            public void Commit(TrieNodeSet nodes)
            {
                Commits.Add(nodes);
                _inner.Commit(nodes);
            }

            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        }

        private sealed class CommitCapturingPathStore : CommitCapturingStore, IRawNodeReader
        {
            private readonly RocksDbPathTrieNodeStore _pathStore;

            public CommitCapturingPathStore(RocksDbPathTrieNodeStore pathStore) : base(pathStore) => _pathStore = pathStore;

            public byte[] TryGetRawNode(byte[] owner, byte[] path) => _pathStore.TryGetRawNode(owner, path);
        }
    }
}
