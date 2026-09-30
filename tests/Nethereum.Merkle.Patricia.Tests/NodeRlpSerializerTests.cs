using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class NodeRlpSerializerTests
    {
        private static readonly IHashProvider Keccak = new Sha3KeccackHashProvider();

        private static byte[] Monkey => "6d6f6e6b6579".HexToByteArray();
        private static byte[] Giraffe => "67697261666665".HexToByteArray();

        [Fact]
        public void Leaf_Encodes_To_Known_Bytes_And_Hash()
        {
            var leaf = new LeafNode();
            leaf.Nibbles = new byte[] { 5, 0, 6 }.ConvertToNibbles();
            leaf.Value = Monkey;

            Assert.Equal("cc8420050006866d6f6e6b6579", leaf.GetEncodedData().ToHex());
            Assert.Equal("1685b970d7cebe8d3e0d77ca46f96ee82e4a5b088ded0976550ac223d4ca1491", leaf.GetHash().ToHex());
        }

        [Fact]
        public void Leaf_RoundTrips_Through_Decoder()
        {
            var leaf = new LeafNode();
            leaf.Nibbles = new byte[] { 5, 0, 6 }.ConvertToNibbles();
            leaf.Value = Monkey;

            var decoded = NodeRlpDecoder.Decode(leaf.GetEncodedData(), null, new byte[0], Keccak);

            var decodedLeaf = Assert.IsType<LeafNode>(decoded);
            Assert.Equal(leaf.Nibbles, decodedLeaf.Nibbles);
            Assert.Equal(leaf.Value, decodedLeaf.Value);
            Assert.Equal(leaf.GetEncodedData(), decodedLeaf.GetEncodedData());
            Assert.Equal(leaf.GetHash(), decodedLeaf.GetHash());
        }

        [Fact]
        public void Branch_With_Embedded_Child_Encodes_To_Known_Bytes_And_RoundTrips()
        {
            var child = new LeafNode();
            child.Nibbles = new byte[] { 5, 0, 6 };
            child.Value = Monkey;

            var branch = new BranchNode();
            branch.SetChild(0, child);
            branch.Value = Giraffe;

            Assert.Equal("e2ca823506866d6f6e6b65798080808080808080808080808080808767697261666665", branch.GetEncodedData().ToHex());
            Assert.Equal("1c06f0682013bded0b69c0fa4e10d356d232b0906478f90e8bf3929ee11fb39c", branch.GetHash().ToHex());

            var decoded = NodeRlpDecoder.Decode(branch.GetEncodedData(), null, new byte[0], Keccak);

            var decodedBranch = Assert.IsType<BranchNode>(decoded);
            Assert.IsType<LeafNode>(decodedBranch.Children[0]);
            Assert.Equal(branch.GetEncodedData(), decodedBranch.GetEncodedData());
            Assert.Equal(branch.GetHash(), decodedBranch.GetHash());
        }

        [Fact]
        public void Extension_With_Hash_Referenced_Child_RoundTrips()
        {
            var child = new LeafNode();
            child.Nibbles = new byte[] { 5, 0, 6 };
            child.Value = Monkey;

            var branch = new BranchNode();
            branch.SetChild(0, child);
            branch.Value = Giraffe;

            var extended = new ExtendedNode();
            extended.InnerNode = branch;
            extended.Nibbles = new byte[] { 0, 1, 0, 2, 0, 3, 0, 4 };

            Assert.Equal("e7850001020304a01c06f0682013bded0b69c0fa4e10d356d232b0906478f90e8bf3929ee11fb39c", extended.GetEncodedData().ToHex());
            Assert.Equal("b8b4b47d9e3dc6f6a2b9e14db7e13be066c9ea959f19c31db2cd11cafa02398a", extended.GetHash().ToHex());

            var decoded = NodeRlpDecoder.Decode(extended.GetEncodedData(), null, new byte[0], Keccak);

            var decodedExtended = Assert.IsType<ExtendedNode>(decoded);
            var hashChild = Assert.IsType<HashNode>(decodedExtended.InnerNode);
            Assert.Equal(branch.GetHash(), hashChild.GetHash());
            Assert.Equal(extended.Nibbles, decodedExtended.Nibbles);
            Assert.Equal(extended.GetEncodedData(), decodedExtended.GetEncodedData());
            Assert.Equal(extended.GetHash(), decodedExtended.GetHash());
        }

        [Fact]
        public void Empty_Encodes_To_Empty_String_Rlp()
        {
            var empty = new EmptyNode();

            Assert.Equal("80", empty.GetEncodedData().ToHex());
            Assert.Equal("56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421", empty.GetHash().ToHex());
        }
    }
}
