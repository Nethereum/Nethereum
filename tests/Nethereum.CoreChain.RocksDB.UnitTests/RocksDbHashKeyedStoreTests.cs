using System;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbHashKeyedStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"hashstore_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;

        public RocksDbHashKeyedStoreTests()
        {
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        public void Dispose()
        {
            _manager.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static int CountKeys(RocksDbManager rocks, string cf)
        {
            using var it = rocks.CreateIterator(cf);
            var n = 0;
            for (it.SeekToFirst(); it.Valid(); it.Next()) n++;
            return n;
        }

        [Fact]
        public void Given_APopulatedHashKeyedStore_When_Cleared_Then_NoNodeIsReadableAndANewNodeCanBeCommitted()
        {
            var store = new RocksDbTrieNodeStore(_manager);
            var first = PathNodeFixtures.ReferenceableNode(null, Array.Empty<byte>(), seed: 7);
            var second = PathNodeFixtures.ReferenceableNode(null, Array.Empty<byte>(), seed: 8);
            store.Commit(PathNodeFixtures.SetOf(first, second));
            Assert.Equal(first.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(first)));

            store.Clear();

            Assert.Null(store.Get(PathNodeFixtures.ReferenceTo(first)));
            Assert.Null(store.Get(PathNodeFixtures.ReferenceTo(second)));
            Assert.Equal(0, CountKeys(_manager, RocksDbManager.CF_TRIE_NODES));
            store.Commit(PathNodeFixtures.SetOf(first));
            Assert.Equal(first.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(first)));
        }

        [Theory]
        [InlineData("hash")]
        [InlineData("path")]
        public void Given_AnIteratorOpenedBeforeAClear_When_TheStoreIsCleared_Then_TheIteratorStillCompletesWithoutError(string flavour)
        {
            var cf = flavour == "hash" ? RocksDbManager.CF_TRIE_NODES : RocksDbManager.CF_STATE_TRIE_ACCOUNT;
            if (flavour == "hash")
                new RocksDbTrieNodeStore(_manager).Commit(PathNodeFixtures.SetOf(
                    PathNodeFixtures.ReferenceableNode(null, Array.Empty<byte>(), seed: 9),
                    PathNodeFixtures.ReferenceableNode(null, Array.Empty<byte>(), seed: 10)));
            else
                new RocksDbPathTrieNodeStore(_manager).Commit(PathNodeFixtures.SetOf(
                    PathNodeFixtures.ReferenceableNode(null, new byte[] { 1 }, seed: 9),
                    PathNodeFixtures.ReferenceableNode(null, new byte[] { 2 }, seed: 10)));
            using var iterator = _manager.CreateIterator(cf);
            iterator.SeekToFirst();

            if (flavour == "hash") new RocksDbTrieNodeStore(_manager).Clear();
            else new RocksDbPathTrieNodeStore(_manager).Clear();

            var visited = 0;
            while (iterator.Valid()) { visited++; iterator.Next(); }
            Assert.InRange(visited, 0, 2);
            Assert.Equal(0, CountKeys(_manager, cf));
        }

        [Fact]
        public void Given_AnObsoleteHashKeyedDeleteRange_When_Called_Then_ItDeletesTheOwnersRowsLikeThePathStore()
        {
            var owner = PathNodeFixtures.Filled(0x55, 32);
            var neighbour = PathNodeFixtures.Filled(0x56, 32);
            var ownerRows = new[] { owner, owner.Concat(new byte[] { 0x01 }).ToArray(), owner.Concat(new byte[] { 0x0A, 0x0B }).ToArray() };
            var neighbourRow = neighbour.Concat(new byte[] { 0x01 }).ToArray();
            foreach (var row in ownerRows.Append(neighbourRow))
                _manager.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, row, new byte[] { 0xAA });

#pragma warning disable CS0618
            new RocksDbTrieNodeStore(_manager).DeleteRange(owner);
#pragma warning restore CS0618

            Assert.All(ownerRows, row => Assert.Null(_manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, row)));
            Assert.NotNull(_manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, neighbourRow));
        }
    }
}
