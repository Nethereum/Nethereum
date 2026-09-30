using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class PatriciaTrieSaveAndCollapseTests
    {
        private static byte[] KeyHash(int i) => new Sha3Keccack().CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
        private static byte[] Value(int i) => new byte[] { (byte)(0x80 | (i & 0x7f)), (byte)(i ^ 0x42), (byte)i };

        private static int CountMaterialized(Node node)
        {
            switch (node)
            {
                case null: return 0;
                case EmptyNode: return 0;
                case HashNode hn: return hn.InnerNode == null ? 0 : CountMaterialized(hn.InnerNode);
                case BranchNode b:
                    int c = 1;
                    foreach (var ch in b.Children) c += CountMaterialized(ch);
                    return c;
                case ExtendedNode e: return 1 + CountMaterialized(e.InnerNode);
                default: return 1;
            }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "patricia-trie", "Periodic save-and-collapse bounds memory without changing the root", Order = 2)]
        [Fact]
        public void RootHash_IsIdentical_WithPeriodicCollapse()
        {
            var refStore = new InMemoryContentNodeStore();
            var refTrie = new PatriciaTrie(refStore);
            for (int i = 0; i < 2000; i++) refTrie.Put(KeyHash(i), Value(i));
            var expected = refTrie.Root.GetHash();

            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            for (int i = 0; i < 2000; i++)
            {
                trie.Put(KeyHash(i), Value(i));
                if (i % 250 == 249) trie.SaveDirtyNodesToStorageAndCollapse();
            }
            var actual = trie.Root.GetHash();

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        public void ReInsertIntoCollapsedBranch_ProducesCorrectRoot()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            for (int i = 0; i < 1000; i++) trie.Put(KeyHash(i), Value(i));
            trie.SaveDirtyNodesToStorageAndCollapse();

            for (int i = 1000; i < 1500; i++) trie.Put(KeyHash(i), Value(i));
            var actual = trie.Root.GetHash();

            var refStore = new InMemoryContentNodeStore();
            var refTrie = new PatriciaTrie(refStore);
            for (int i = 0; i < 1500; i++) refTrie.Put(KeyHash(i), Value(i));
            Assert.Equal(refTrie.Root.GetHash().ToHex(), actual.ToHex());
        }

        [Fact]
        public void Collapse_BoundsResidentNodeCount()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            for (int i = 0; i < 2000; i++) trie.Put(KeyHash(i), Value(i));

            int before = CountMaterialized(trie.Root);
            trie.SaveDirtyNodesToStorageAndCollapse();
            int after = CountMaterialized(trie.Root);

            Assert.True(before > 100, $"expected a large resident trie before collapse, got {before}");
            Assert.True(after <= 2, $"expected the resident set to collapse to ~the root, got {after}");
        }

        [Fact]
        public void Get_WorksAfterCollapse_ViaLazyLoad()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            for (int i = 0; i < 1000; i++) trie.Put(KeyHash(i), Value(i));
            trie.SaveDirtyNodesToStorageAndCollapse();

            for (int i = 0; i < 1000; i++)
                Assert.Equal(Value(i).ToHex(), trie.Get(KeyHash(i)).ToHex());
        }
    }
}
