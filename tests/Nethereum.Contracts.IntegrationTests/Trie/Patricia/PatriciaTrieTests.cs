using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Trie.Patricia
{

    public class PatriciaTrieTests
    {
        [Fact]
        public void TrieTest()
        {
          var trie = new PatriciaTrie();
          trie.Put(new byte[] { 1, 2, 3, 4 }, new StringByteArrayConvertor().ConvertToByteArray("monkey"));
          trie.Put(new byte[] {1,2}, new StringByteArrayConvertor().ConvertToByteArray("giraffe"));
          var hash = trie.Root.GetHash();
          Assert.True(hash.ToHex().IsTheSameHex("a02d89d1c0a595eecbcbee8b30c7c677be66b2314bc2661e163f1349868f45c7"));
        }


        [Fact]
        public void TrieTest2()
        {
            var trie = new PatriciaTrie();
            trie.Put(new byte[] { 1, 2, 3, 4 }, new StringByteArrayConvertor().ConvertToByteArray("monkey"));
            var rlp = trie.Root.GetRLPEncodedData().ToHex();
            trie.Put(new byte[] { 1, 2 }, new StringByteArrayConvertor().ConvertToByteArray("giraffe"));
            trie.Put(new byte[] { 1, 2 }, new StringByteArrayConvertor().ConvertToByteArray("elephant"));
            rlp = trie.Root.GetRLPEncodedData().ToHex();
            var hash = trie.Root.GetHash();
            Assert.True(hash.ToHex().IsTheSameHex("f249e880b1b8af8e788411e0cf26313cdfedb4388250f64ef10bea45ef76f9d1"));
        }

      
        [Fact]
        public void ShouldMakeAProof()
        {
            var trie = new PatriciaTrie();
            trie.Put(new byte[] { 1, 2, 3, }, new StringByteArrayConvertor().ConvertToByteArray("monkey"));
            trie.Put(new byte[] { 1, 2, 3, 4, 5 }, new StringByteArrayConvertor().ConvertToByteArray("giraffe"));
            
            
            var storage = trie.GenerateProof(new byte[] { 1, 2, 3 });
            var node = new NodeDecoder().DecodeNode(trie.Root.GetHash(), false, storage);
            Assert.True(node.GetHash().ToHex().IsTheSameHex(trie.Root.GetHash().ToHex()));
            node = new NodeDecoder().DecodeNode(trie.Root.GetHash(), true, storage);
            Assert.True(node.GetHash().ToHex().IsTheSameHex(trie.Root.GetHash().ToHex()));


            var trie2 = new PatriciaTrie(trie.Root.GetHash(), new Sha3KeccackHashProvider());
            var value = trie2.Get(new byte[] { 1, 2, 3, }, storage);
            Assert.True(value.ToHex().IsTheSameHex(new StringByteArrayConvertor().ConvertToByteArray("monkey").ToHex()));
            Assert.True(trie2.Root.GetHash().ToHex().IsTheSameHex(trie.Root.GetHash().ToHex()));
        }

        [Fact]
        public void TrieTestPutLeafNode()
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 1, 2, 3, 4 };
            var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            trie.Put(key, value);
            var leafNode = new LeafNode();
            leafNode.Nibbles = key.ConvertToNibbles();
            leafNode.Value = value;
            var trieHash = trie.Root.GetHash();
            var leafNodeHash = leafNode.GetHash();
            Assert.True(trieHash.ToHex().IsTheSameHex("f6ec9fe71a6649f422350f383ff0e2e33b42a2941b1c95599f145e1e3697b864"));
            Assert.True(leafNodeHash.ToHex().IsTheSameHex("f6ec9fe71a6649f422350f383ff0e2e33b42a2941b1c95599f145e1e3697b864"));
        }

        [Fact]
        public void TrieTestPutLeafNodeAndThenShorterKeyThatCreatesBranch()
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 1, 2, 3, 4 };
            var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            trie.Put(key, value);

            var key2 = new byte[] { 1, 2, 3 };
            var value2 = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
            trie.Put(key2, value2);

            var leafNode = new LeafNode
            {
                Nibbles = new byte[] { 4 },
                Value = value
            };

            var branchNode = new BranchNode();
            branchNode.SetChild(0, leafNode);
            branchNode.Value = value2;

            var extendedNode = new ExtendedNode
            {
                InnerNode = branchNode,
                Nibbles = key2.ConvertToNibbles()
            };

            var trieHash = trie.Root.GetHash();
            var extendedNodeHash = extendedNode.GetHash();
            Assert.True(trieHash.ToHex().IsTheSameHex("3b8255bc1fb241a4e8eef2bebc2b783ad3aed8da7a5ceb06db39bda447be1531"));
            Assert.True(extendedNodeHash.ToHex().IsTheSameHex("3b8255bc1fb241a4e8eef2bebc2b783ad3aed8da7a5ceb06db39bda447be1531"));

        }

        [Fact]
        public void TrieTestPutLeafNodeWithExtraNibblesKey()
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 1, 2, 3, 4 };
            var key2 = new byte[] { 1, 2, 3, 4, 5, 6 };
            var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var value2 = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
            trie.Put(key, value);
            trie.Put(key2, value2);

            //leaf node with extra nibbles
            var leafNode = new LeafNode
            {
                Nibbles = new byte[] { 5, 0, 6 },
                Value = value2
            };

            var branchNode = new BranchNode();
            branchNode.SetChild(0, leafNode);
            branchNode.Value = value;

            var extendedNode = new ExtendedNode
            {
                InnerNode = branchNode,
                Nibbles = new byte[] { 0, 1, 0, 2, 0, 3, 0, 4 }
            };

            var trieHash = trie.Root.GetHash();
            var extendedNodeHash = extendedNode.GetHash();
            Assert.True(trieHash.ToHex().IsTheSameHex("424d6999533a2f79e3cc0b77e7a97caf3201e35e7077b9ebae480a993cc0a8cd"));
            Assert.True(extendedNodeHash.ToHex().IsTheSameHex("424d6999533a2f79e3cc0b77e7a97caf3201e35e7077b9ebae480a993cc0a8cd"));
        }


        [Fact]
        public void TrieTestPut3Values3KeysPrefix3NiblesTheSame()
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 1, 2, 3, 4 };
            var key2 = new byte[] { 1, 2, 3, 5 };
            var key3 = new byte[] { 1, 2, 3};
            var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var value2 = new StringByteArrayConvertor().ConvertToByteArray("elephant");
            var value3 = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
            trie.Put(key, value);
            trie.Put(key2, value2);
            trie.Put(key3, value3);

            //leaf node with extra nibbles
            var leafNode1 = new LeafNode
            {
                Nibbles = new byte[0],
                Value = value
            };

            var leafNode2 = new LeafNode
            {
                Nibbles = new byte[0],
                Value = value2
            };

            var branchNode = new BranchNode();
            branchNode.SetChild(4, leafNode1);
            branchNode.SetChild(5, leafNode2);


            var branchNode2 = new BranchNode();
            branchNode2.Value = value3;
            branchNode2.SetChild(0, branchNode);

            var extendedNode = new ExtendedNode
            {
                InnerNode = branchNode2,
                Nibbles = new byte[] { 0, 1, 0, 2, 0, 3}
            };

            var trieHash = trie.Root.GetHash();
            var extendedNodeHash = extendedNode.GetHash();
            Assert.True(trieHash.ToHex().IsTheSameHex("295466a3e47ea76436839ef8a8e4d089ecc524415052215ce49ce12cd382cca0"));
            Assert.True(extendedNodeHash.ToHex().IsTheSameHex("295466a3e47ea76436839ef8a8e4d089ecc524415052215ce49ce12cd382cca0"));
        }


        [Fact]
        public void TrieTestPut3Values3KeysPrefix2NibblesTheSame()
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 1, 2, 3, 4 };
            var key2 = new byte[] { 1, 2, 3, 5 };
            var key3 = new byte[] { 1, 2, 5 };
            var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var value2 = new StringByteArrayConvertor().ConvertToByteArray("elephant");
            var value3 = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
            trie.Put(key, value);
            trie.Put(key2, value2);
            trie.Put(key3, value3);

            //leaf node with extra nibbles
            var leafNode1 = new LeafNode
            {
                Nibbles = new byte[0],
                Value = value
            };

            var leafNode2 = new LeafNode
            {
                Nibbles = new byte[0],
                Value = value2
            };

            var branchNode = new BranchNode();
            branchNode.SetChild(4, leafNode1);
            branchNode.SetChild(5, leafNode2);

            var extendedNode1 = new ExtendedNode
            {
                InnerNode = branchNode,
                Nibbles = new byte[] {0}
            };

            var branchNode2 = new BranchNode();
            branchNode2.SetChild(3, extendedNode1);

            var leafNode3 = new LeafNode
            {
                Nibbles = new byte[0],
                Value = value3
            };

            branchNode2.SetChild(5, leafNode3);

            var extendedNode = new ExtendedNode
            {
                InnerNode = branchNode2,
                Nibbles = new byte[] { 0, 1, 0, 2, 0}
            };

            var trieHash = trie.Root.GetHash();
            var extendedNodeHash = extendedNode.GetHash();
            Assert.True(trieHash.ToHex().IsTheSameHex("bd8d1a50efd5a795c0e51aea0bc0dbcb04b8f141bba2ad95fef38531d173e962"));
            Assert.True(extendedNodeHash.ToHex().IsTheSameHex("bd8d1a50efd5a795c0e51aea0bc0dbcb04b8f141bba2ad95fef38531d173e962"));
        }

        [Fact]
        public void EmptyNodeTest()
        {
            var emptyNode = new EmptyNode();
            
            var encodedData = emptyNode.GetRLPEncodedData();
            var hash = emptyNode.GetHash();
            Assert.True("56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".IsTheSameHex(hash.ToHex()));
            Assert.True("56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".IsTheSameHex(new Sha3KeccackHashProvider().ComputeHash(encodedData).ToHex()));
        }

        [Fact]
        public void LeafNodeTest()
        {

            var node = new LeafNode();
            node.Nibbles = new byte[] { 1, 2, 3, 4 }.ConvertToNibbles();
            node.Value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var hash = node.GetHash();
            Assert.True(hash.ToHex().IsTheSameHex("f6ec9fe71a6649f422350f383ff0e2e33b42a2941b1c95599f145e1e3697b864"));
        }

        [Fact] 
        public void LeafNodeTest2()
        {
            var node = new LeafNode();
            node.Nibbles = new byte[] { 5, 0, 6}.ConvertToNibbles();
            node.Value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var hexNibbles = node.Nibbles.ToHex();
            var hexNibblesPrefixed = node.GetPrefixedNibbles().ToHex();
            var hexNibblesPrefixedAsBytes = node.GetPrefixedNibbles().ConvertFromNibbles().ToHex();
            var rlp = node.GetRLPEncodedData().ToHex();
            var hash = node.GetHash();
            Assert.True("cc8420050006866d6f6e6b6579".IsTheSameHex(rlp));
            Assert.True(hash.ToHex().IsTheSameHex("1685b970d7cebe8d3e0d77ca46f96ee82e4a5b088ded0976550ac223d4ca1491"));
        }



        [Fact] 
        public void BranchNodeTest() 
        {
            var node = new LeafNode();
            node.Nibbles = new byte[] { 5, 0, 6};
            node.Value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var nodeHex = node.GetRLPEncodedData().ToHex();
            var branchNode = new BranchNode();
            branchNode.SetChild(0, node);
            branchNode.Value = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
            var encoded = branchNode.GetRLPEncodedData().ToHex();
            Assert.True("e2ca823506866d6f6e6b65798080808080808080808080808080808767697261666665".IsTheSameHex(encoded));
            var hash = branchNode.GetHash().ToHex();
            Assert.True(hash.IsTheSameHex("1c06f0682013bded0b69c0fa4e10d356d232b0906478f90e8bf3929ee11fb39c"));
        }


        [Fact]
        public void ExtensionTest()
        {

            var node = new LeafNode();
            node.Nibbles = new byte[] { 5, 0, 6 };
            node.Value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
            var nodeHex = node.GetRLPEncodedData().ToHex();
            var branchNode = new BranchNode();
            branchNode.SetChild(0, node);
            branchNode.Value = new StringByteArrayConvertor().ConvertToByteArray("giraffe");

            var extended = new ExtendedNode();
            extended.InnerNode = branchNode;
            extended.Nibbles = new byte[] { 0, 1, 0, 2, 0, 3, 0, 4 };

            var encoded = extended.GetRLPEncodedData().ToHex();
            
            Assert.True("e7850001020304a01c06f0682013bded0b69c0fa4e10d356d232b0906478f90e8bf3929ee11fb39c".IsTheSameHex(encoded));
            var hash = extended.GetHash().ToHex();
            Assert.True(hash.IsTheSameHex("b8b4b47d9e3dc6f6a2b9e14db7e13be066c9ea959f19c31db2cd11cafa02398a"));

        }

        [Fact]
        public void LeafNodeNibbleTest()
        {
            var node = new LeafNode();
            node.Nibbles = new byte[] { 3, 0, 6 };
            var prefixed = node.GetPrefixedNibbles();
            Assert.Equal(new byte[] { 3, 3, 0, 6 }, prefixed);

            node.Nibbles = new byte[] { 8, 3, 0, 6 };
            prefixed = node.GetPrefixedNibbles();
            Assert.Equal(new byte[] { 2, 0, 8, 3, 0, 6 }, prefixed);
        }


        [Fact]
        public void NibleExtensionTest()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            Assert.Equal(bytes, bytes.ConvertToNibbles().ConvertFromNibbles());

            bytes = new byte[] { 12, 21, 32, 45 };
            Assert.Equal(bytes, bytes.ConvertToNibbles().ConvertFromNibbles());

            bytes = new byte[] { 1, 50 };
            Assert.Equal(new byte[] { 0, 1, 3, 2 }, bytes.ConvertToNibbles());
        }
    }
}