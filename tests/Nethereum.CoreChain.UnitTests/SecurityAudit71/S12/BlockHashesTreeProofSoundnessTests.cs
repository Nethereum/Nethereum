using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.DataAvailability;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S12
{
    public class BlockHashesTreeProofSoundnessTests
    {
        private static List<byte[]> Hashes(int n) =>
            Enumerable.Range(1, n).Select(i => Enumerable.Repeat((byte)i, 32).ToArray()).ToList();

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [Trait("Rule", "S12-60")]
        public void Given_a_valid_proof_when_the_claimed_index_is_out_of_range_then_it_is_rejected(int count)
        {
            var hashes = Hashes(count);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var last = count - 1;
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, last, out _);
            Assert.True(BlockHashesTree.VerifyProof(root, hashes[last], last, proof, count));

            for (int bad = count; bad < count + 4; bad++)
                Assert.False(BlockHashesTree.VerifyProof(root, hashes[last], bad, proof, count));
        }

        [Fact]
        [Trait("Rule", "S12-60")]
        public void Given_a_valid_proof_when_the_claimed_index_is_negative_then_it_is_rejected_without_throwing()
        {
            var hashes = Hashes(5);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 2, out _);

            bool result = true;
            var ex = Record.Exception(() => result = BlockHashesTree.VerifyProof(root, hashes[2], -1, proof, 5));
            Assert.Null(ex);
            Assert.False(result);
        }

        [Fact]
        [Trait("Rule", "S12-60")]
        public void Given_a_valid_proof_when_the_claimed_index_is_another_valid_index_then_it_is_rejected()
        {
            var hashes = Hashes(8);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 2, out _);

            for (int i = 0; i < 8; i++)
                if (i != 2)
                    Assert.False(BlockHashesTree.VerifyProof(root, hashes[2], i, proof, 8));
        }

        [Fact]
        [Trait("Rule", "S12-61")]
        public void Given_a_proof_whose_length_is_not_a_multiple_of_32_when_verified_then_it_is_rejected_without_throwing()
        {
            var hashes = Hashes(8);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 2, out _);
            var ragged = proof.Take(proof.Length - 5).ToArray();

            bool result = true;
            var ex = Record.Exception(() => result = BlockHashesTree.VerifyProof(root, hashes[2], 2, ragged, 8));
            Assert.Null(ex);
            Assert.False(result);
        }

        [Fact]
        [Trait("Rule", "S12-61")]
        public void Given_a_null_proof_or_null_hash_when_verified_then_it_is_rejected_without_throwing()
        {
            var hashes = Hashes(4);
            var root = BlockHashesTree.ComputeRoot(hashes);

            bool a = true, b = true;
            var ex1 = Record.Exception(() => a = BlockHashesTree.VerifyProof(root, hashes[0], 0, null, 4));
            var ex2 = Record.Exception(() => b = BlockHashesTree.VerifyProof(root, null, 0, new byte[64], 4));
            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.False(a);
            Assert.False(b);
        }

        [Fact]
        [Trait("Rule", "S12-62")]
        public void Given_a_valid_proof_when_it_is_truncated_or_extended_by_whole_nodes_then_it_is_rejected()
        {
            var hashes = Hashes(8);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 5, out _);

            Assert.False(BlockHashesTree.VerifyProof(root, hashes[5], 5, proof.Take(proof.Length - 32).ToArray(), 8));
            Assert.False(BlockHashesTree.VerifyProof(root, hashes[5], 5, proof.Concat(new byte[32]).ToArray(), 8));
        }

        [Fact]
        [Trait("Rule", "S12-62")]
        public void Given_a_valid_proof_when_total_leaves_is_misreported_then_it_is_rejected()
        {
            var hashes = Hashes(5);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 4, out _);

            Assert.False(BlockHashesTree.VerifyProof(root, hashes[4], 4, proof, 6));
            Assert.False(BlockHashesTree.VerifyProof(root, hashes[4], 4, proof, 8));
        }
    }
}
