using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nethereum.Ssz.Tests
{
    public class SszMerkleizerVerifyProofStrictTests
    {
        private const int ChunkSize = 32;

        [Fact]
        public void Given_BranchCountEqualsDepth_When_VerifyProof_Then_VerifiesCorrectly()
        {
            var leaf = Enumerable.Repeat((byte)0x01, ChunkSize).ToArray();
            var sibling = Enumerable.Repeat((byte)0x02, ChunkSize).ToArray();
            var branch = new List<byte[]> { sibling };
            const int depth = 1;
            const int index = 0;

            var expectedRoot = HashPair(leaf, sibling);

            var result = SszMerkleizer.VerifyProof(leaf, branch, depth, index, expectedRoot);

            Assert.True(result);
        }

        [Fact]
        public void Given_BranchCountGreaterThanDepth_When_VerifyProof_Then_ReturnsFalse()
        {
            var leaf = Enumerable.Repeat((byte)0x01, ChunkSize).ToArray();
            var sibling = Enumerable.Repeat((byte)0x02, ChunkSize).ToArray();
            var extra = Enumerable.Repeat((byte)0x03, ChunkSize).ToArray();
            var branch = new List<byte[]> { sibling, extra };
            const int depth = 1;
            const int index = 0;

            var rootIfTruncated = HashPair(leaf, sibling);

            var result = SszMerkleizer.VerifyProof(leaf, branch, depth, index, rootIfTruncated);

            Assert.False(result);
        }

        [Fact]
        public void Given_BranchCountLessThanDepth_When_VerifyProof_Then_ReturnsFalse()
        {
            var leaf = Enumerable.Repeat((byte)0x01, ChunkSize).ToArray();
            var sibling = Enumerable.Repeat((byte)0x02, ChunkSize).ToArray();
            var branch = new List<byte[]> { sibling };
            const int depth = 2;
            const int index = 0;
            var root = Enumerable.Repeat((byte)0xFF, ChunkSize).ToArray();

            var result = SszMerkleizer.VerifyProof(leaf, branch, depth, index, root);

            Assert.False(result);
        }

        [Fact]
        public void Given_BranchNull_When_VerifyProof_Then_ReturnsFalse()
        {
            var leaf = Enumerable.Repeat((byte)0x01, ChunkSize).ToArray();
            var root = Enumerable.Repeat((byte)0xFF, ChunkSize).ToArray();

            var result = SszMerkleizer.VerifyProof(leaf, null, 1, 0, root);

            Assert.False(result);
        }

        [Theory]
        [InlineData(2, 3)]
        [InlineData(3, 5)]
        [InlineData(5, 7)]
        public void Given_BranchCountExceedsDepthByOne_When_VerifyProof_Then_ReturnsFalse(int depth, int branchCount)
        {
            var leaf = Enumerable.Repeat((byte)0x01, ChunkSize).ToArray();
            var branch = new List<byte[]>();
            for (var i = 0; i < branchCount; i++)
            {
                branch.Add(Enumerable.Repeat((byte)(0x10 + i), ChunkSize).ToArray());
            }
            var root = Enumerable.Repeat((byte)0xAA, ChunkSize).ToArray();

            var result = SszMerkleizer.VerifyProof(leaf, branch, depth, 0, root);

            Assert.False(result);
        }

        private static byte[] HashPair(byte[] left, byte[] right)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var concat = new byte[left.Length + right.Length];
            System.Buffer.BlockCopy(left, 0, concat, 0, left.Length);
            System.Buffer.BlockCopy(right, 0, concat, left.Length, right.Length);
            return sha.ComputeHash(concat);
        }
    }
}
