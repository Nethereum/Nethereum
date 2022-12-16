using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle;
using Nethereum.Merkle.ByteConvertors;
using Nethereum.Merkle.HashProviders;
using System.Linq;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.MerkleDropTests
{
    public class MerkleUnitTests
    {
        [Fact]
        public void SimpleMerkleTest()
        {
            var elements = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray().ToList();
            var merkleTree = new MerkleTree<char>(new Sha3KeccackHashProvider(), new ChartByteArrayConvertor());
            merkleTree.BuildTree(elements);
            var hexRoot = merkleTree.Root.Hash.ToHex(true);
            Assert.Equal("0xec0dffcb601ee38fa372bbf1d89ed16761db0a0b215480032b783f8c33230783", hexRoot);

            var proofs = merkleTree.GetProof('A');
            var expected = new[] {
  "0x1f675bff07515f5df96737194ea945c36c41e7b4fcef307b7cd4d0e602a69111",
  "0xe62e1dfc08d58fd144947903447473a090c958fe34e2425d578237fcdf1ab5a4",
  "0x1907ce7877ec74782a26c166b562bfbdd4c8d8833f98ad82ae9dc8e98db20093",
  "0x511e1752d2beef1a4401200cd90356c2c613a4634904c588a0554e411b584d9d",
  "0x5321fd6247e1697c052d1ee2c054903b15aa3558c6597d1e6509c7f1c7e25501",
  "0xa206b020fa6e2ef9e35d21d831cc00a586b9d210a4aa57dd334303654bb0a3d8",
  "0xf30c17f6c257181e11b9ea19fc7d498b2880fcad645a66e130edeab084271f16" };
            for (int i = 0; i < proofs.Count; i++)
            {
                Assert.True(expected[i].IsTheSameHex(proofs[i].ToHex()));
            }

            Assert.True(merkleTree.VerifyProof(proofs, 'A'));
        }
    }
}