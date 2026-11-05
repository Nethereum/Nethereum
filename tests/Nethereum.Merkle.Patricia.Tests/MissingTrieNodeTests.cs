using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class MissingTrieNodeTests
    {
        private static readonly byte[] Owner = Enumerable.Repeat((byte)0x77, 32).ToArray();

        private static byte[] Key(byte first, byte second)
        {
            var key = new byte[32];
            key[0] = first;
            key[1] = second;
            return key;
        }

        private static (byte[] root, InMemoryContentNodeStore full) BuildFull()
        {
            var full = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(full, Owner);
            for (byte i = 0; i < 40; i++)
                trie.Put(Key((byte)(i * 6), i), Enumerable.Repeat((byte)(i + 1), 40).ToArray());
            trie.SaveDirtyNodesToStorage();
            return (trie.Root.GetHash(), full);
        }

        [Fact]
        public void Given_a_hash_node_whose_node_is_missing_from_the_store_When_the_trie_is_read_Then_MissingTrieNodeException_names_owner_path_and_hash()
        {
            var (root, full) = BuildFull();
            var rootOnly = new InMemoryContentNodeStore();
            rootOnly.Put(root, full.Get(root));
            var trie = PatriciaTrie.LoadFromStorage(root, rootOnly, Owner);

            var thrown = Assert.Throws<MissingTrieNodeException>(() => trie.Get(Key(0, 0)));

            Assert.Equal(Owner, thrown.Owner);
            Assert.NotEmpty(thrown.Path);
            Assert.Equal(32, thrown.Hash.Length);
            Assert.Contains(full.Storage.Keys, k => ByteUtil.AreEqual(k, thrown.Hash));
            Assert.Null(rootOnly.Get(thrown.Hash));
            Assert.Contains(thrown.Hash.ToHex(true), thrown.Message);
            Assert.Contains(thrown.Owner.ToHex(true), thrown.Message);
            Assert.Contains(thrown.Path.ToHex(true), thrown.Message);
        }

        [Fact]
        public void Given_the_same_trie_with_every_node_present_When_the_trie_is_read_Then_the_value_is_returned()
        {
            var (root, full) = BuildFull();
            var trie = PatriciaTrie.LoadFromStorage(root, full, Owner);

            var value = trie.Get(Key(0, 0));

            Assert.Equal(Enumerable.Repeat((byte)1, 40).ToArray(), value);
        }

        [Fact]
        public void Given_a_trie_opened_at_the_empty_root_with_an_empty_store_When_read_Then_it_is_empty()
        {
            var empty = new InMemoryContentNodeStore();
            var trie = PatriciaTrie.LoadFromStorage(DefaultValues.EMPTY_TRIE_HASH, empty, Owner);

            Assert.Null(trie.Get(Key(1, 2)));
        }
    }
}
