using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Util;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class CommitmentTests
    {
        private static readonly PoseidonHasher HasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
        private static readonly PoseidonHasher HasherT2 = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
        private static readonly PoseidonHasher HasherT3 = new PoseidonHasher(PoseidonParameterPreset.CircomT3);

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void Create_ProducesConsistentNullifierHash()
        {
            var nullifier = new BigInteger(12345);
            var secret = new BigInteger(67890);
            var value = new BigInteger(1000000000000000000);
            var label = new BigInteger(42);

            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            var expectedNullifierHash = HasherT1.Hash(nullifier);
            Assert.Equal(expectedNullifierHash, commitment.NullifierHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void Create_ProducesConsistentPrecommitment()
        {
            var nullifier = new BigInteger(12345);
            var secret = new BigInteger(67890);
            var value = new BigInteger(1000000000000000000);
            var label = new BigInteger(42);

            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            var expectedPrecommitment = HasherT2.Hash(nullifier, secret);
            Assert.Equal(expectedPrecommitment, commitment.Precommitment);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void Create_ProducesConsistentCommitmentHash()
        {
            var nullifier = new BigInteger(12345);
            var secret = new BigInteger(67890);
            var value = new BigInteger(1000000000000000000);
            var label = new BigInteger(42);

            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            var precommitment = HasherT2.Hash(nullifier, secret);
            var expectedCommitmentHash = HasherT3.Hash(value, label, precommitment);
            Assert.Equal(expectedCommitmentHash, commitment.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        [NethereumDocExample(DocSection.Protocols, "commitments", "Create and verify Poseidon commitments")]
        public void Create_FullChain_T1_T2_T3()
        {
            var nullifier = new BigInteger(111);
            var secret = new BigInteger(222);
            var value = new BigInteger(333);
            var label = new BigInteger(444);

            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            var nullifierHash = HasherT1.Hash(nullifier);
            var precommitment = HasherT2.Hash(nullifier, secret);
            var commitmentHash = HasherT3.Hash(value, label, precommitment);

            Assert.Equal(nullifierHash, commitment.NullifierHash);
            Assert.Equal(precommitment, commitment.Precommitment);
            Assert.Equal(commitmentHash, commitment.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void Create_StoresInputValues()
        {
            var nullifier = new BigInteger(100);
            var secret = new BigInteger(200);
            var value = new BigInteger(300);
            var label = new BigInteger(400);

            var commitment = PrivacyPoolCommitment.Create(value, label, nullifier, secret);

            Assert.Equal(value, commitment.Value);
            Assert.Equal(label, commitment.Label);
            Assert.Equal(nullifier, commitment.Nullifier);
            Assert.Equal(secret, commitment.Secret);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void CreateRandom_ProducesDifferentCommitments()
        {
            var value = new BigInteger(1000000000000000000);
            var label = new BigInteger(42);

            var c1 = PrivacyPoolCommitment.CreateRandom(value, label);
            var c2 = PrivacyPoolCommitment.CreateRandom(value, label);

            Assert.NotEqual(c1.Nullifier, c2.Nullifier);
            Assert.NotEqual(c1.Secret, c2.Secret);
            Assert.NotEqual(c1.CommitmentHash, c2.CommitmentHash);
            Assert.NotEqual(c1.NullifierHash, c2.NullifierHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void CreateRandom_FieldElementsAreWithinField()
        {
            var value = new BigInteger(1);
            var label = new BigInteger(1);

            var commitment = PrivacyPoolCommitment.CreateRandom(value, label);

            Assert.True(commitment.Nullifier >= BigInteger.Zero);
            Assert.True(commitment.Nullifier < PrivacyPoolConstants.SNARK_SCALAR_FIELD);
            Assert.True(commitment.Secret >= BigInteger.Zero);
            Assert.True(commitment.Secret < PrivacyPoolConstants.SNARK_SCALAR_FIELD);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void SameInputs_ProduceSameOutputs()
        {
            var c1 = PrivacyPoolCommitment.Create(100, 200, 300, 400);
            var c2 = PrivacyPoolCommitment.Create(100, 200, 300, 400);

            Assert.Equal(c1.NullifierHash, c2.NullifierHash);
            Assert.Equal(c1.Precommitment, c2.Precommitment);
            Assert.Equal(c1.CommitmentHash, c2.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void DifferentValues_ProduceDifferentCommitments()
        {
            var c1 = PrivacyPoolCommitment.Create(100, 200, 300, 400);
            var c2 = PrivacyPoolCommitment.Create(101, 200, 300, 400);

            Assert.Equal(c1.NullifierHash, c2.NullifierHash);
            Assert.Equal(c1.Precommitment, c2.Precommitment);
            Assert.NotEqual(c1.CommitmentHash, c2.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void DifferentNullifiers_ProduceDifferentNullifierHashes()
        {
            var c1 = PrivacyPoolCommitment.Create(100, 200, 300, 400);
            var c2 = PrivacyPoolCommitment.Create(100, 200, 301, 400);

            Assert.NotEqual(c1.NullifierHash, c2.NullifierHash);
            Assert.NotEqual(c1.Precommitment, c2.Precommitment);
            Assert.NotEqual(c1.CommitmentHash, c2.CommitmentHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void NullifierHash_IsNonZero()
        {
            var commitment = PrivacyPoolCommitment.Create(1, 2, 3, 4);
            Assert.NotEqual(BigInteger.Zero, commitment.NullifierHash);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-Commitment")]
        public void CommitmentHash_IsWithinField()
        {
            var commitment = PrivacyPoolCommitment.Create(
                BigInteger.Parse("1000000000000000000"),
                BigInteger.Parse("42"),
                BigInteger.Parse("123456789012345678901234567890"),
                BigInteger.Parse("987654321098765432109876543210"));

            Assert.True(commitment.CommitmentHash >= BigInteger.Zero);
            Assert.True(commitment.CommitmentHash < PrivacyPoolConstants.SNARK_SCALAR_FIELD);
        }
    }
}
