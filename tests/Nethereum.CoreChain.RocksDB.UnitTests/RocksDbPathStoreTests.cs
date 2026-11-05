using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPathStoreTests : IDisposable
    {
        private static readonly byte[] Row = new byte[] { 0xAA };
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
            var dir = Path.Combine(Path.GetTempPath(), $"pathstore_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            return bundle;
        }

        private static void Wipe(RocksDbChainStoreBundle bundle, string route, byte[] owner)
        {
            if (route == "store")
            {
                new RocksDbPathTrieNodeStore(bundle.Rocks).DeleteRange(owner);
                return;
            }
            var sink = ((IHealNodeSinkProvider)bundle).CreateHealSink();
            sink.ForceWipeStorage(owner);
            sink.Flush();
        }

        private static void PutRows(RocksDbChainStoreBundle bundle, params byte[][] keys)
        {
            foreach (var key in keys) bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, key, Row);
        }

        private static bool Exists(RocksDbChainStoreBundle bundle, byte[] key)
            => bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, key) != null;

        private static int CountKeys(RocksDbManager rocks, string cf)
        {
            using var it = rocks.CreateIterator(cf);
            var n = 0;
            for (it.SeekToFirst(); it.Valid(); it.Next()) n++;
            return n;
        }

        [Theory]
        [InlineData("store")]
        [InlineData("sink")]
        public void Given_AnOwnerOfAll0xFFBytes_When_ItsStorageTrieIsWiped_Then_OnlyThatOwnersNodesAreDeletedAndNeighboursSurvive(string route)
        {
            var bundle = OpenPathKeyedBundle();
            var owner = PathNodeFixtures.Filled(0xFF, 32);
            var below = PathNodeFixtures.Filled(0xFF, 32);
            below[31] = 0xFE;
            var ownerRows = new[]
            {
                owner,
                owner.Concat(new byte[] { 0x01, 0x02 }).ToArray(),
                owner.Concat(PathNodeFixtures.Filled(0xFF, 40)).ToArray()
            };
            var belowRow = below.Concat(new byte[] { 0x01 }).ToArray();
            var lowRow = PathNodeFixtures.Filled(0x01, 32);
            PutRows(bundle, ownerRows.Concat(new[] { below, belowRow, lowRow }).ToArray());

            Wipe(bundle, route, owner);

            Assert.All(ownerRows, row => Assert.False(Exists(bundle, row)));
            Assert.True(Exists(bundle, below));
            Assert.True(Exists(bundle, belowRow));
            Assert.True(Exists(bundle, lowRow));
        }

        [Theory]
        [InlineData("store")]
        [InlineData("sink")]
        public void Given_AnOwnerEndingIn0xFF_When_ItsStorageTrieIsWiped_Then_TheNextOwnersFirstNodeIsNotDeleted(string route)
        {
            var bundle = OpenPathKeyedBundle();
            var owner = PathNodeFixtures.Filled(0x01, 32);
            owner[31] = 0xFF;
            var next = PathNodeFixtures.Filled(0x01, 32);
            next[30] = 0x02;
            next[31] = 0x00;
            var ownerRows = new[] { owner, owner.Concat(new byte[] { 0x0F, 0x0F }).ToArray() };
            var nextRows = new[] { next, next.Concat(new byte[] { 0x00 }).ToArray() };
            PutRows(bundle, ownerRows.Concat(nextRows).ToArray());

            Wipe(bundle, route, owner);

            Assert.All(ownerRows, row => Assert.False(Exists(bundle, row)));
            Assert.All(nextRows, row => Assert.True(Exists(bundle, row)));
        }

        [Fact]
        public void Given_APathStoreNodeWithNullPath_When_CommittedAndRead_Then_ItIsStoredAtTheOwnerPrefixOnly()
        {
            var bundle = OpenPathKeyedBundle();
            var store = new RocksDbPathTrieNodeStore(bundle.Rocks);
            var owner = PathNodeFixtures.Filled(0x33, 32);
            var storageNode = PathNodeFixtures.ReferenceableNode(owner, null, seed: 1);
            var accountNode = PathNodeFixtures.ReferenceableNode(null, null, seed: 2);

            store.Commit(PathNodeFixtures.SetOf(storageNode, accountNode));

            Assert.Equal(storageNode.GetEncodedData(), bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, owner));
            Assert.Equal(1, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_STORAGE));
            Assert.Equal(accountNode.GetEncodedData(), bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>()));
            Assert.Equal(1, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_ACCOUNT));
            Assert.Equal(storageNode.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(storageNode)));
            Assert.Equal(accountNode.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(accountNode)));
        }

        [Fact]
        public void Given_APopulatedPathKeyedStore_When_Cleared_Then_BothColumnFamiliesAreEmptyAndAcceptNewCommits()
        {
            var bundle = OpenPathKeyedBundle();
            var store = new RocksDbPathTrieNodeStore(bundle.Rocks);
            var accountNode = PathNodeFixtures.ReferenceableNode(null, new byte[] { 1 }, seed: 3);
            var storageNode = PathNodeFixtures.ReferenceableNode(PathNodeFixtures.Filled(0x44, 32), new byte[] { 2 }, seed: 4);
            store.Commit(PathNodeFixtures.SetOf(accountNode, storageNode));
            Assert.Equal(1, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_ACCOUNT));
            Assert.Equal(1, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_STORAGE));

            store.Clear();

            Assert.Equal(0, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_ACCOUNT));
            Assert.Equal(0, CountKeys(bundle.Rocks, RocksDbManager.CF_STATE_TRIE_STORAGE));
            Assert.Null(store.Get(PathNodeFixtures.ReferenceTo(accountNode)));
            store.Commit(PathNodeFixtures.SetOf(accountNode, storageNode));
            Assert.Equal(accountNode.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(accountNode)));
            Assert.Equal(storageNode.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(storageNode)));
        }
    }
}
