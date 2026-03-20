using System;
using System.IO;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests
{
    public class ExternalVectorTests
    {
        private static string TestDataPath(string circuit, string file) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", circuit, file);

        private static (string proof, string vk, string pub) LoadCircuit(string name) =>
        (
            File.ReadAllText(TestDataPath(name, "proof.json")),
            File.ReadAllText(TestDataPath(name, "verification_key.json")),
            File.ReadAllText(TestDataPath(name, "public.json"))
        );

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void Square_Circuit_x7_y49_ValidProof()
        {
            var (proof, vk, pub) = LoadCircuit("square");
            var result = CircomGroth16Adapter.Verify(proof, vk, pub);
            Assert.True(result.IsValid, result.Error ?? "Square proof (x=7,y=49) should be valid");
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void Square_Circuit_TamperedOutput_Invalid()
        {
            var (proofJson, vkJson, _) = LoadCircuit("square");
            var proof = SnarkjsProofParser.Parse(proofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);

            var wrongInputs = new BigInteger[]
            {
                new BigInteger("50"),
                new BigInteger("7")
            };

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, vk, wrongInputs);
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void Square_Circuit_CorrectICLength()
        {
            var (_, vkJson, pubJson) = LoadCircuit("square");
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
            var pub = SnarkjsPublicInputParser.Parse(pubJson);

            Assert.Equal(3, vk.IC.Length);
            Assert.Equal(2, pub.Length);
            Assert.Equal(vk.IC.Length - 1, pub.Length);
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void ThreeInputs_Circuit_5x13plus42_eq107_ValidProof()
        {
            var (proof, vk, pub) = LoadCircuit("threeinputs");
            var result = CircomGroth16Adapter.Verify(proof, vk, pub);
            Assert.True(result.IsValid, result.Error ?? "ThreeInputs proof (5*13+42=107) should be valid");
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void ThreeInputs_Circuit_TamperedInput_Invalid()
        {
            var (proofJson, vkJson, _) = LoadCircuit("threeinputs");
            var proof = SnarkjsProofParser.Parse(proofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);

            var wrongInputs = new BigInteger[]
            {
                new BigInteger("107"),
                new BigInteger("6"),
                new BigInteger("13"),
                new BigInteger("42")
            };

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, vk, wrongInputs);
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void ThreeInputs_Circuit_CorrectICLength()
        {
            var (_, vkJson, pubJson) = LoadCircuit("threeinputs");
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
            var pub = SnarkjsPublicInputParser.Parse(pubJson);

            Assert.Equal(5, vk.IC.Length);
            Assert.Equal(4, pub.Length);
            Assert.Equal(vk.IC.Length - 1, pub.Length);
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void CrossCircuit_ProofFromSquare_VkFromThreeInputs_Invalid()
        {
            var (squareProofJson, _, squarePubJson) = LoadCircuit("square");
            var (_, threeVkJson, _) = LoadCircuit("threeinputs");

            var proof = SnarkjsProofParser.Parse(squareProofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(threeVkJson);
            var pub = SnarkjsPublicInputParser.Parse(squarePubJson);

            var verifier = new Groth16Verifier();
            var result = verifier.Verify(proof, vk, pub);
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Category", "ZK-ExternalVector")]
        public void AllCircuits_DifferentTrustedSetups_IndependentVerification()
        {
            var circuits = new[] { "square", "threeinputs" };
            foreach (var circuit in circuits)
            {
                var (proof, vk, pub) = LoadCircuit(circuit);
                var result = CircomGroth16Adapter.Verify(proof, vk, pub);
                Assert.True(result.IsValid, $"Circuit '{circuit}' should verify: {result.Error}");
            }
        }
    }
}
