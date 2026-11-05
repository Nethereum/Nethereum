using System.Linq;
using System.Numerics;
using Nethereum.Merkle;
using Xunit;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S12
{
    public class PoseidonMerkleSoundnessTests
    {
        private static readonly BigInteger Field =
            BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        private static PoseidonMerkleTree Build()
        {
            var tree = new PoseidonMerkleTree();
            tree.InsertCommitments(Enumerable.Range(1, 5).Select(i => new BigInteger(i) * 1000003));
            return tree;
        }

        [Fact]
        [Trait("Rule", "S12-40")]
        public void Given_an_inclusion_proof_when_the_commitment_is_offset_by_the_field_modulus_then_it_is_rejected()
        {
            var tree = Build();
            var commitment = new BigInteger(2) * 1000003;
            var proof = tree.GenerateInclusionProof(1);
            Assert.True(tree.VerifyInclusionProof(proof, commitment));

            Assert.False(tree.VerifyInclusionProof(proof, commitment + Field));
        }

        [Fact]
        [Trait("Rule", "S12-40")]
        public void Given_an_inclusion_proof_when_the_commitment_is_offset_by_two_to_the_256_then_it_is_rejected()
        {
            var tree = Build();
            var commitment = new BigInteger(2) * 1000003;
            var proof = tree.GenerateInclusionProof(1);

            Assert.False(tree.VerifyInclusionProof(proof, commitment + BigInteger.Pow(2, 256)));
        }

        [Fact]
        [Trait("Rule", "S12-40")]
        public void Given_an_inclusion_proof_when_the_commitment_is_negative_then_it_does_not_throw_and_is_rejected()
        {
            var tree = Build();
            var proof = tree.GenerateInclusionProof(1);
            bool result = true;
            var ex = Record.Exception(() => result = tree.VerifyInclusionProof(proof, BigInteger.MinusOne));
            Assert.Null(ex);
            Assert.False(result);
        }

        [Fact]
        [Trait("Rule", "S12-42")]
        public void Given_an_inclusion_proof_when_the_path_indices_are_missing_then_it_is_rejected()
        {
            var tree = Build();
            var commitment = new BigInteger(4) * 1000003;
            var proof = tree.GenerateInclusionProof(3);
            var stripped = new MerkleProof { ProofNodes = proof.ProofNodes, PathIndices = null };
            Assert.True(tree.VerifyInclusionProof(proof, commitment));
            Assert.False(tree.VerifyInclusionProof(stripped, commitment));
        }
    }
}
