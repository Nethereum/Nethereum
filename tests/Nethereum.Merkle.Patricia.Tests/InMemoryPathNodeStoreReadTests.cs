using System;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class InMemoryPathNodeStoreReadTests
    {
        private static Node ReferenceableNode(byte[] owner, byte[] path)
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            var keccak = new Sha3Keccack();
            for (int i = 0; i < 60; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var node = trie.Root;
            node.Owner = owner;
            node.Path = path;
            Assert.True(node.GetEncodedData().Length >= 32);
            return node;
        }

        private static TrieNodeSet SetOf(Node node)
        {
            var set = new TrieNodeSet();
            set.Add(node);
            return set;
        }

        private static HashNode ReferenceTo(Node node, byte[] owner, byte[] path)
            => new HashNode { Hash = node.GetHash(), Owner = owner, Path = path };

        [Fact]
        public void Given_APathStoreHoldingATamperedBlob_When_ReadByReference_Then_AnInvalidOperationExceptionNamesTheHashMismatch()
        {
            var node = ReferenceableNode(null, new byte[] { 4, 5 });
            var store = new InMemoryPathNodeStore();
            store.Commit(SetOf(node));

            var tamperedReference = new HashNode { Hash = new byte[32], Owner = null, Path = new byte[] { 4, 5 } };

            var refusal = Assert.Throws<InvalidOperationException>(() => store.Get(tamperedReference));
            Assert.Contains("hash mismatch", refusal.Message);
        }

        [Fact]
        public void Given_AMissingNode_When_ReadByReference_Then_NullIsReturnedWithoutThrowing()
        {
            var store = new InMemoryPathNodeStore();

            var missing = new HashNode { Hash = new byte[32], Owner = null, Path = new byte[] { 7 } };

            Assert.Null(store.Get(missing));
            Assert.False(store.Contains(missing));
            Assert.Null(store.Get(null));
        }

        [Fact]
        public void Given_AStoreWhoseRootBlobDoesNotHashToTheRequestedRoot_When_ContainsKeyIsAsked_Then_ItIsFalseForEveryStoreFlavour()
        {
            var root = ReferenceableNode(null, new byte[0]);
            var store = new InMemoryPathNodeStore();
            store.Commit(SetOf(root));

            Assert.True(store.ContainsKey(root.GetHash()));
            Assert.False(store.ContainsKey(new byte[32]));
            Assert.False(new InMemoryPathNodeStore().ContainsKey(root.GetHash()));
        }

        [Fact]
        public void Given_ARootThatIsNot32Bytes_When_ContainsKeyIsAsked_Then_ItIsFalse()
        {
            var root = ReferenceableNode(null, new byte[0]);
            var store = new InMemoryPathNodeStore();
            store.Commit(SetOf(root));

            Assert.False(store.ContainsKey(null));
            Assert.False(store.ContainsKey(new byte[0]));
            Assert.False(store.ContainsKey(new byte[31]));
            Assert.False(store.ContainsKey(new byte[33]));
        }

        [Fact]
        public void Given_AnEmptyOwner_When_AccountTrieNodesAreCommitted_Then_ReadsReturnTheBlobAndMutatingTheCallersPathDoesNotMoveTheEntry()
        {
            var callersPath = new byte[] { 1, 2, 3 };
            var node = ReferenceableNode(new byte[0], callersPath);
            var blob = node.GetEncodedData();
            var store = new InMemoryPathNodeStore();

            store.Commit(SetOf(node));
            callersPath[0] = 9;

            var reference = ReferenceTo(node, new byte[0], new byte[] { 1, 2, 3 });
            Assert.True(store.Contains(reference));
            Assert.Equal(blob, store.Get(reference));
        }
    }
}
