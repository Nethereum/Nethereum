using System.Linq;
using Nethereum.DevChain.Storage.Sqlite;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class SqliteTrieNodeStoreKeyComparerTests
    {
        private static byte[] Key(params byte[] bytes) => bytes.ToArray();

        [Fact]
        public void Given_EqualButDistinctKeyArrays_When_TrieNodeStoreIsUsed_Then_TheyResolveToOneEntryAndTrailingZeroKeysStaySeparate()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);
            var store = new SqliteTrieNodeStore(manager);

            store.Put(Key(1, 2, 3), Key(0xAA));
            store.Put(Key(1, 2, 3), Key(0xBB));
            store.Put(Key(1, 2, 3, 0), Key(0xCC));

            Assert.Equal(Key(0xBB), store.Get(Key(1, 2, 3)));
            Assert.Equal(Key(0xCC), store.Get(Key(1, 2, 3, 0)));
            Assert.True(store.ContainsKey(Key(1, 2, 3)));
            Assert.False(store.ContainsKey(Key(1, 2)));

            store.Flush();
            var reopened = new SqliteTrieNodeStore(manager);
            Assert.Equal(Key(0xBB), reopened.Get(Key(1, 2, 3)));
            Assert.Equal(Key(0xCC), reopened.Get(Key(1, 2, 3, 0)));

            store.Delete(Key(1, 2, 3));
            Assert.Null(store.Get(Key(1, 2, 3)));
            Assert.Equal(Key(0xCC), store.Get(Key(1, 2, 3, 0)));
        }
    }
}
