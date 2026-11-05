using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.DataAvailability;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S12.Round2
{
    public class BlockHashesTreeRound2Tests
    {
        private static List<byte[]> Hashes(int n) =>
            Enumerable.Range(1, n).Select(i => System.Linq.Enumerable.Repeat((byte)i, 32).ToArray()).ToList();

        [Fact]
        [Trait("Rule", "S12-R2-30")]
        public void Given_trees_of_every_size_up_to_48_then_every_honest_proof_verifies_and_no_other_index_or_count_does()
        {
            for (int n = 1; n <= 48; n++)
            {
                var hashes = Hashes(n);
                var root = BlockHashesTree.ComputeRoot(hashes);
                for (int i = 0; i < n; i++)
                {
                    var proof = BlockHashesTree.ComputeMerkleProof(hashes, i, out _);
                    Assert.True(BlockHashesTree.VerifyProof(root, hashes[i], i, proof, n), "n=" + n + " i=" + i);

                    for (int j = -2; j < n + 3; j++)
                        if (j != i)
                            Assert.False(BlockHashesTree.VerifyProof(root, hashes[i], j, proof, n), "n=" + n + " i=" + i + " j=" + j);
                }
            }
        }

        [Fact]
        [Trait("AcceptedRisk", "S12-R2-3")]
        [Trait("Rule", "S12-R2-30")]
        public void Given_a_misreported_total_leaves_then_a_nonexistent_position_can_verify_and_the_honest_total_never_does()
        {
            var misreportedAccepted = 0;
            for (int n = 1; n <= 48; n++)
            {
                var hashes = Hashes(n);
                var root = BlockHashesTree.ComputeRoot(hashes);
                for (int i = 0; i < n; i++)
                {
                    var proof = BlockHashesTree.ComputeMerkleProof(hashes, i, out _);
                    for (int pos = n; pos < n + 4; pos++)
                        Assert.False(BlockHashesTree.VerifyProof(root, hashes[i], pos, proof, n));
                    for (int claimed = 1; claimed <= 64; claimed++)
                        for (int pos = n; pos < claimed; pos++)
                            if (BlockHashesTree.VerifyProof(root, hashes[i], pos, proof, claimed))
                                misreportedAccepted++;
                }
            }
            Assert.True(misreportedAccepted > 0);
        }

        [Fact]
        [Trait("Rule", "S12-R2-31")]
        public void Given_a_two_leaf_tree_when_a_huge_total_leaves_is_claimed_then_a_nonexistent_index_is_not_accepted()
        {
            var hashes = Hashes(2);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 1, out _);

            foreach (var total in new[] { int.MaxValue, int.MaxValue - 1, 1 << 30 })
            {
                foreach (var index in new[] { 3, 1001, 1000001, 1 << 20 })
                {
                    bool result = true;
                    var ex = Record.Exception(() => result = BlockHashesTree.VerifyProof(root, hashes[1], index, proof, total));
                    Assert.Null(ex);
                    Assert.False(result, "total=" + total + " index=" + index);
                }
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-31")]
        public void Given_non_positive_total_leaves_then_verification_is_rejected_without_throwing()
        {
            var hashes = Hashes(2);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 0, out _);
            foreach (var total in new[] { int.MinValue, 0, -1 })
                Assert.False(BlockHashesTree.VerifyProof(root, hashes[0], 0, proof, total));
        }

        [Fact]
        [Trait("Rule", "S12-R2-32")]
        public void Given_a_single_leaf_tree_then_the_empty_proof_verifies_only_for_index_zero()
        {
            var hashes = Hashes(1);
            var root = BlockHashesTree.ComputeRoot(hashes);
            var proof = BlockHashesTree.ComputeMerkleProof(hashes, 0, out _);

            Assert.True(BlockHashesTree.VerifyProof(root, hashes[0], 0, proof, 1));
            Assert.False(BlockHashesTree.VerifyProof(root, hashes[0], 1, proof, 1));
        }
    }
}
