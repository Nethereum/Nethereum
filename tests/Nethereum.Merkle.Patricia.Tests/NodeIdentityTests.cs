using Nethereum.Merkle.Patricia;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class NodeIdentityTests
    {
        [Fact]
        public void Owner_And_Path_Live_On_The_Base_Node()
        {
            var owner = new byte[] { 0xAA };
            var path = new byte[] { 0x01, 0x02 };

            Node leaf = new LeafNode { Owner = owner, Path = path };
            Assert.Equal(owner, leaf.Owner);
            Assert.Equal(path, leaf.Path);

            Node branch = new BranchNode { Owner = owner, Path = path };
            Assert.Equal(owner, branch.Owner);
            Assert.Equal(path, branch.Path);

            Node hash = new HashNode { Owner = owner, Path = path };
            Assert.Equal(owner, hash.Owner);
            Assert.Equal(path, hash.Path);
        }
    }
}
