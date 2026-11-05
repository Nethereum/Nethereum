using System.Linq;
using Nethereum.DevChain.Storage.Sqlite;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class SqliteBinaryTrieNodeStoreKeyComparerTests
    {
        private static byte[] Key(params byte[] bytes) => bytes.ToArray();

        [Fact]
        public void Given_EqualButDistinctHashArrays_When_BinaryNodesAreWritten_Then_TheyShareOneDirtyEntryAndTrailingZeroHashesStaySeparate()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);
            var store = new SqliteBinaryTrieNodeStore(manager);

            store.PutNode(Key(7, 8, 9), Key(0xAA), 1, 2, null);
            store.PutNode(Key(7, 8, 9), Key(0xBB), 1, 2, null);

            var dirty = store.GetDirtyNodes();
            Assert.Single(dirty);
            Assert.Equal(Key(0xBB), dirty[0].Encoded);

            store.PutNode(Key(7, 8, 9, 0), Key(0xCC), 1, 2, null);

            Assert.Equal(2, store.GetDirtyNodes().Count);
            Assert.Equal(2, store.NodeCount);
            Assert.Equal(Key(0xBB), store.Get(Key(7, 8, 9)));
            Assert.Equal(Key(0xCC), store.Get(Key(7, 8, 9, 0)));

            store.Delete(Key(7, 8, 9));

            var remaining = store.GetDirtyNodes();
            Assert.Single(remaining);
            Assert.Equal(Key(7, 8, 9, 0), remaining[0].Hash);
            Assert.Null(store.Get(Key(7, 8, 9)));
        }
    }
}
