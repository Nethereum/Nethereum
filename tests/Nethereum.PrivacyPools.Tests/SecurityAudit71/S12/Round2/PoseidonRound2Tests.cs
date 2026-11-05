using System;
using System.Linq;
using System.Numerics;
using Nethereum.Merkle;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S12.Round2
{
    public class PoseidonRound2Tests
    {
        private static readonly BigInteger Field =
            BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        private readonly ITestOutputHelper _output;

        public PoseidonRound2Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static PoseidonMerkleTree Build(int n = 5)
        {
            var tree = new PoseidonMerkleTree();
            tree.InsertCommitments(Enumerable.Range(1, n).Select(i => new BigInteger(i) * 1000003));
            return tree;
        }

        [Fact]
        [Trait("Rule", "S12-R2-40")]
        public void Given_boundary_values_then_zero_and_modulus_minus_one_are_accepted_as_leaves_and_the_modulus_is_rejected()
        {
            var tree = new PoseidonMerkleTree();
            tree.InsertCommitment(BigInteger.Zero);
            tree.InsertCommitment(Field - 1);
            Assert.Throws<ArgumentException>(() => tree.InsertCommitment(Field));
            Assert.ThrowsAny<ArgumentException>(() => tree.InsertCommitment(BigInteger.MinusOne));
            Assert.ThrowsAny<ArgumentException>(() => tree.InsertCommitments(new[] { BigInteger.One, Field }));
            Assert.Equal(2, tree.Size);
        }

        [Fact]
        [Trait("Rule", "S12-R2-41")]
        public void Given_a_tree_when_inserting_a_batch_with_one_invalid_value_then_no_leaf_is_added()
        {
            var tree = Build(3);
            var before = tree.Size;
            Assert.ThrowsAny<ArgumentException>(() => tree.InsertCommitments(new[] { BigInteger.One, Field + 5 }));
            Assert.Equal(before, tree.Size);
        }

        [Fact]
        [Trait("Rule", "S12-R2-42")]
        public void Given_a_proof_when_a_sibling_or_the_root_is_outside_the_field_then_verification_is_false()
        {
            var tree = Build();
            var commitment = new BigInteger(2) * 1000003;
            var proof = tree.GenerateInclusionProof(1);
            Assert.True(tree.VerifyInclusionProof(proof, commitment));

            var big = Field.ToByteArray(isUnsigned: true, isBigEndian: true);
            var siblingOver = new MerkleProof
            {
                ProofNodes = proof.ProofNodes.Select((n, i) => i == 0 ? big : n).ToList(),
                PathIndices = proof.PathIndices
            };
            Assert.False(tree.VerifyInclusionProof(siblingOver, commitment));

            var rootOver = Enumerable.Repeat((byte)0xFF, 32).ToArray();
            Assert.False(tree.VerifyInclusionProof(proof, commitment, rootOver));
            Assert.False(tree.VerifyInclusionProof(proof, commitment, new byte[33]));
            Assert.False(tree.VerifyInclusionProof(proof, commitment, null));
            Assert.False(tree.VerifyInclusionProof(null, commitment));
        }

        [Fact]
        [Trait("Rule", "S12-R2-43")]
        public void Given_an_exported_tree_when_imported_with_a_leaf_outside_the_field_then_the_import_is_rejected()
        {
            var tree = Build(4);
            var json = tree.Export();
            _output.WriteLine(json);

            var over = (Field + 3).ToByteArray(isUnsigned: true, isBigEndian: true);
            var original = (new BigInteger(2) * 1000003).ToByteArray(isUnsigned: true, isBigEndian: true);
            var padded = new byte[32];
            Array.Copy(original, 0, padded, 32 - original.Length, original.Length);
            var originalB64 = Convert.ToBase64String(padded);
            Assert.Contains(originalB64, json);

            var forged = json.Replace(originalB64, Convert.ToBase64String(over));

            PoseidonMerkleTree imported = null;
            var ex = Record.Exception(() => imported = PoseidonMerkleTree.Import(forged, verifyIntegrity: true));
            Assert.True(ex != null, "tampered import accepted");
        }

        [Fact]
        [Trait("Rule", "S12-R2-44")]
        public void Given_an_internal_node_value_presented_as_a_commitment_with_a_shortened_proof_then_the_behaviour_is_recorded()
        {
            var tree = Build(4);
            var leafZero = tree.GenerateInclusionProof(0);
            var internalNode = new BigInteger(tree.GenerateInclusionProof(2).ProofNodes[1], isUnsigned: true, isBigEndian: true);
            var shortened = new MerkleProof
            {
                ProofNodes = leafZero.ProofNodes.Skip(1).ToList(),
                PathIndices = leafZero.PathIndices.Skip(1).ToList()
            };
            _output.WriteLine("internal-as-leaf accepted=" + tree.VerifyInclusionProof(shortened, internalNode));
        }
    }
}
