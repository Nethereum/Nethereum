using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockHashesTreePinTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();
        private static readonly byte[] DomainTag = Keccak.CalculateHash(Encoding.UTF8.GetBytes("appchain:v1:blockhashes"));

        private static byte[] Hash(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

        private static byte[] Leaf(byte[] hash) => Keccak.CalculateHash(DomainTag.Concat(hash ?? Array.Empty<byte>()).ToArray());

        private static byte[] Pair(byte[] left, byte[] right) => Keccak.CalculateHash(left.Concat(right).ToArray());

        [Fact]
        public void Given_NullBlockHashInList_When_ComputingRoot_Then_RootUnchangedFromBaseline()
        {
            var hashes = new List<byte[]> { Hash(1), null, Hash(3) };

            var expected = Pair(Pair(Leaf(Hash(1)), Leaf(null)), Leaf(Hash(3)));

            Assert.Equal(expected, BlockHashesTree.ComputeRoot(hashes));
            Assert.Equal(new byte[32], BlockHashesTree.ComputeRoot(new List<byte[]>()));
            Assert.Equal(new byte[32], BlockHashesTree.ComputeRoot(null));
            Assert.Equal(Leaf(Hash(9)), BlockHashesTree.ComputeRoot(new List<byte[]> { Hash(9) }));
        }

        [Fact]
        public void Given_FourLeaves_When_ProofVerified_Then_VerifyProofReturnsTrueForEachIndex()
        {
            var hashes = new List<byte[]> { Hash(1), Hash(2), Hash(3), Hash(4) };
            var root = BlockHashesTree.ComputeRoot(hashes);

            for (int i = 0; i < hashes.Count; i++)
            {
                var proof = BlockHashesTree.ComputeMerkleProof(hashes, i, out _);
                Assert.True(BlockHashesTree.VerifyProof(root, hashes[i], i, proof, hashes.Count));
            }
        }

        [Fact]
        public void Given_ThreeLeaves_When_ProofVerified_Then_VerifyProofReturnsTrueForEachIndex()
        {
            var hashes = new List<byte[]> { Hash(1), Hash(2), Hash(3) };
            var root = BlockHashesTree.ComputeRoot(hashes);

            for (int i = 0; i < hashes.Count; i++)
            {
                var proof = BlockHashesTree.ComputeMerkleProof(hashes, i, out var leaf);
                Assert.Equal(Leaf(hashes[i]), leaf);
                Assert.True(BlockHashesTree.VerifyProof(root, hashes[i], i, proof, hashes.Count), $"leaf index {i}");
                Assert.False(BlockHashesTree.VerifyProof(root, Hash(0xEE), i, proof, hashes.Count));
            }
            Assert.Throws<ArgumentException>(() => BlockHashesTree.ComputeMerkleProof(hashes, 3, out _));
        }
    }
}
