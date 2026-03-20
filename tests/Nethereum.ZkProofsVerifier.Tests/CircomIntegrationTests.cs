using System;
using System.IO;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests
{
    public class CircomIntegrationTests
    {
        private static string GetTestDataPath(string filename) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", filename);

        private string ProofJson => File.ReadAllText(GetTestDataPath("proof.json"));
        private string VkJson => File.ReadAllText(GetTestDataPath("verification_key.json"));
        private string PublicJson => File.ReadAllText(GetTestDataPath("public.json"));

        [Fact]
        [Trait("Category", "ZK-Integration")]
        public void Verify_ValidMultiplierProof_ReturnsValid()
        {
            var result = CircomGroth16Adapter.Verify(ProofJson, VkJson, PublicJson);

            Assert.True(result.IsValid, result.Error ?? "Expected valid proof");
        }

        [Fact]
        [Trait("Category", "ZK-Integration")]
        public void Verify_TamperedPublicInput_ReturnsInvalid()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(VkJson);
            var proof = SnarkjsProofParser.Parse(ProofJson);

            var wrongInputs = new BigInteger[]
            {
                new BigInteger("34"),
                new BigInteger("3"),
                new BigInteger("11")
            };

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, vk, wrongInputs);

            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Category", "ZK-Integration")]
        public void Verify_TamperedProofA_ReturnsInvalid()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(VkJson);
            var proof = SnarkjsProofParser.Parse(ProofJson);
            var publicInputs = SnarkjsPublicInputParser.Parse(PublicJson);

            var tamperedA = proof.A.Negate();
            var tamperedProof = new Groth16Proof { A = tamperedA, B = proof.B, C = proof.C };

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(tamperedProof, vk, publicInputs);

            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Category", "ZK-Integration")]
        public void Verify_StepByStep_ParseAndVerify()
        {
            var proof = SnarkjsProofParser.Parse(ProofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(VkJson);
            var publicInputs = SnarkjsPublicInputParser.Parse(PublicJson);

            Assert.False(proof.A.IsInfinity);
            Assert.False(proof.B.IsInfinity());
            Assert.False(proof.C.IsInfinity);
            Assert.Equal(4, vk.IC.Length);
            Assert.Equal(3, publicInputs.Length);

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, vk, publicInputs);

            Assert.True(result.IsValid, result.Error ?? "Expected valid proof");
        }

        [Fact]
        [Trait("Category", "ZK-Integration")]
        public void Verify_WrongVerificationKey_ReturnsInvalid()
        {
            var proof = SnarkjsProofParser.Parse(ProofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(VkJson);
            var publicInputs = SnarkjsPublicInputParser.Parse(PublicJson);

            var wrongVk = new Groth16VerificationKey
            {
                Alpha = vk.Alpha.Negate(),
                Beta = vk.Beta,
                Gamma = vk.Gamma,
                Delta = vk.Delta,
                IC = vk.IC
            };

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, wrongVk, publicInputs);

            Assert.False(result.IsValid);
        }
    }
}
