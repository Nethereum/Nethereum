using System.Linq;
using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Util;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class MerkleTreePoseidonTests
    {
        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void EmptyTree_HasZeroRoot()
        {
            var tree = new PoseidonMerkleTree();
            Assert.Equal(BigInteger.Zero, tree.RootAsBigInteger);
            Assert.Equal(0, tree.Size);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void InsertSingleCommitment_UpdatesRoot()
        {
            var tree = new PoseidonMerkleTree();
            var commitment = PrivacyPoolCommitment.Create(1, 2, 3, 4);
            tree.InsertCommitment(commitment.CommitmentHash);

            Assert.Equal(1, tree.Size);
            Assert.NotEqual(BigInteger.Zero, tree.RootAsBigInteger);
            Assert.Equal(32, tree.Root.Length);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void InsertTwoCommitments_MatchesManualCalculation()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
            var tree = new PoseidonMerkleTree();

            var c1 = PrivacyPoolCommitment.Create(100, 1, 10, 20);
            var c2 = PrivacyPoolCommitment.Create(200, 2, 30, 40);

            tree.InsertCommitment(c1.CommitmentHash);
            tree.InsertCommitment(c2.CommitmentHash);

            Assert.Equal(2, tree.Size);
            Assert.Equal(1, tree.Depth);

            var leaf1 = c1.CommitmentHash.ToByteArray(isUnsigned: true, isBigEndian: true);
            var leaf2 = c2.CommitmentHash.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (leaf1.Length < 32) { var p = new byte[32]; System.Array.Copy(leaf1, 0, p, 32 - leaf1.Length, leaf1.Length); leaf1 = p; }
            if (leaf2.Length < 32) { var p = new byte[32]; System.Array.Copy(leaf2, 0, p, 32 - leaf2.Length, leaf2.Length); leaf2 = p; }
            var expectedRoot = hasher.HashBytesToBytes(leaf1, leaf2);

            Assert.Equal(expectedRoot, tree.Root);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void InsertBatch_MatchesSequential()
        {
            var tree1 = new PoseidonMerkleTree();
            var tree2 = new PoseidonMerkleTree();

            var commitments = Enumerable.Range(1, 8)
                .Select(i => PrivacyPoolCommitment.Create(i * 100, i, i * 10, i * 20).CommitmentHash)
                .ToList();

            foreach (var c in commitments)
                tree1.InsertCommitment(c);

            tree2.InsertCommitments(commitments);

            Assert.Equal(tree1.Root, tree2.Root);
            Assert.Equal(tree1.Size, tree2.Size);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        [NethereumDocExample(DocSection.Protocols, "merkle-tree", "Generate and verify Merkle inclusion proofs")]
        public void GenerateAndVerifyInclusionProof_AllLeaves()
        {
            var tree = new PoseidonMerkleTree();
            var commitments = Enumerable.Range(1, 4)
                .Select(i => PrivacyPoolCommitment.Create(i * 100, i, i * 10, i * 20).CommitmentHash)
                .ToList();

            tree.InsertCommitments(commitments);

            for (int i = 0; i < commitments.Count; i++)
            {
                var proof = tree.GenerateInclusionProof(i);
                Assert.True(tree.VerifyInclusionProof(proof, commitments[i]),
                    $"Inclusion proof failed for commitment at index {i}");
            }
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void VerifyInclusionProof_WrongCommitment_Fails()
        {
            var tree = new PoseidonMerkleTree();
            var c1 = PrivacyPoolCommitment.Create(100, 1, 10, 20);
            var c2 = PrivacyPoolCommitment.Create(200, 2, 30, 40);

            tree.InsertCommitment(c1.CommitmentHash);
            tree.InsertCommitment(c2.CommitmentHash);

            var proof = tree.GenerateInclusionProof(0);
            Assert.False(tree.VerifyInclusionProof(proof, c2.CommitmentHash));
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void GetProofSiblings_ReturnsCorrectCount()
        {
            var tree = new PoseidonMerkleTree();
            var commitments = Enumerable.Range(1, 4)
                .Select(i => (BigInteger)(i * 100))
                .ToList();

            tree.InsertCommitments(commitments);

            var proof = tree.GenerateInclusionProof(0);
            var siblings = tree.GetProofSiblings(proof);
            var pathIndices = tree.GetProofPathIndices(proof);

            Assert.Equal(proof.ProofNodes.Count, siblings.Length);
            Assert.Equal(proof.PathIndices.Count, pathIndices.Length);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void DeterministicRoot_SameCommitments()
        {
            var tree1 = new PoseidonMerkleTree();
            var tree2 = new PoseidonMerkleTree();

            var commitments = new BigInteger[] { 100, 200, 300 };
            foreach (var c in commitments)
            {
                tree1.InsertCommitment(c);
                tree2.InsertCommitment(c);
            }

            Assert.Equal(tree1.Root, tree2.Root);
            Assert.Equal(tree1.RootAsBigInteger, tree2.RootAsBigInteger);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void RootAsBigInteger_MatchesBytesConversion()
        {
            var tree = new PoseidonMerkleTree();
            tree.InsertCommitment(new BigInteger(42));

            var rootFromBytes = new BigInteger(tree.Root, isUnsigned: true, isBigEndian: true);
            Assert.Equal(rootFromBytes, tree.RootAsBigInteger);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-MerkleTree")]
        public void SixteenLeaves_AllProofsValid()
        {
            var tree = new PoseidonMerkleTree();
            var commitments = Enumerable.Range(1, 16)
                .Select(i => PrivacyPoolCommitment.CreateRandom(i * 100, i).CommitmentHash)
                .ToList();

            tree.InsertCommitments(commitments);

            Assert.Equal(16, tree.Size);
            Assert.Equal(4, tree.Depth);

            for (int i = 0; i < commitments.Count; i++)
            {
                var proof = tree.GenerateInclusionProof(i);
                Assert.True(tree.VerifyInclusionProof(proof, commitments[i]),
                    $"Proof failed for leaf {i}");
            }
        }
    }
}
