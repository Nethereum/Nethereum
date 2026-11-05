using System;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    internal static class PathNodeFixtures
    {
        public static Node ReferenceableNode(byte[] owner, byte[] path, int seed = 0)
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            var keccak = new Sha3Keccack();
            for (int i = 0; i < 60; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)seed, (byte)i }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var node = trie.Root;
            node.Owner = owner;
            node.Path = path;
            Assert.True(node.GetEncodedData().Length >= 32);
            return node;
        }

        public static TrieNodeSet SetOf(params Node[] nodes)
        {
            var set = new TrieNodeSet();
            foreach (var node in nodes) set.Add(node);
            return set;
        }

        public static HashNode ReferenceTo(Node node)
            => new HashNode { Hash = node.GetHash(), Owner = node.Owner, Path = node.Path };

        public static HashNode ReferenceAt(byte[] hash, byte[] owner, byte[] path)
            => new HashNode { Hash = hash, Owner = owner, Path = path };

        public static byte[] Filled(byte fill, int length)
        {
            var bytes = new byte[length];
            Array.Fill(bytes, fill);
            return bytes;
        }
    }
}
