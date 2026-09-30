using Nethereum.Merkle.Patricia;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class ITrieNodeStoreContentAddressedTests
    {
        [Fact]
        public void Commit_Then_Resolve_By_Reference()
        {
            var backing = new InMemoryContentNodeStore();
            ITrieNodeStore store = new ContentAddressedNodeStore(backing);

            var leaf = new LeafNode { Nibbles = new byte[] { 1, 2, 3 }, Value = new byte[] { 0xAB } };
            var extended = new ExtendedNode { Nibbles = new byte[] { 4 }, InnerNode = new HashNode { Hash = leaf.GetHash() } };

            var set = new TrieNodeSet();
            set.Add(leaf);
            set.Add(extended);
            store.Commit(set);

            var leafRef = new HashNode { Hash = leaf.GetHash() };
            Assert.Equal(leaf.GetEncodedData(), store.Get(leafRef));
            Assert.True(store.Contains(leafRef));

            var extRef = new HashNode { Hash = extended.GetHash() };
            Assert.Equal(extended.GetEncodedData(), store.Get(extRef));

            Assert.False(store.Contains(new HashNode { Hash = new byte[32] }));
        }
    }
}
