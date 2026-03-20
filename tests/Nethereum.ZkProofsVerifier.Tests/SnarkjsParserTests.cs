using System;
using System.IO;
using Nethereum.ZkProofsVerifier.Circom;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests
{
    public class SnarkjsParserTests
    {
        private static string GetTestDataPath(string filename) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", filename);

        [Fact]
        public void ParseProof_ValidJson_ReturnsCorrectPoints()
        {
            var json = File.ReadAllText(GetTestDataPath("proof.json"));
            var proof = SnarkjsProofParser.Parse(json);

            Assert.NotNull(proof.A);
            Assert.NotNull(proof.B);
            Assert.NotNull(proof.C);
            Assert.False(proof.A.IsInfinity);
            Assert.False(proof.B.IsInfinity());
            Assert.False(proof.C.IsInfinity);
        }

        [Fact]
        public void ParseVerificationKey_ValidJson_ReturnsCorrectStructure()
        {
            var json = File.ReadAllText(GetTestDataPath("verification_key.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(json);

            Assert.NotNull(vk.Alpha);
            Assert.NotNull(vk.Beta);
            Assert.NotNull(vk.Gamma);
            Assert.NotNull(vk.Delta);
            Assert.NotNull(vk.IC);
            Assert.Equal(4, vk.IC.Length);
        }

        [Fact]
        public void ParsePublicInputs_ValidJson_ReturnsCorrectValues()
        {
            var json = File.ReadAllText(GetTestDataPath("public.json"));
            var inputs = SnarkjsPublicInputParser.Parse(json);

            Assert.Equal(3, inputs.Length);
            Assert.Equal("33", inputs[0].ToString());
            Assert.Equal("3", inputs[1].ToString());
            Assert.Equal("11", inputs[2].ToString());
        }

        [Fact]
        public void ParseProof_EmptyJson_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => SnarkjsProofParser.Parse(""));
        }

        [Fact]
        public void ParseProof_NullJson_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => SnarkjsProofParser.Parse(null));
        }

        [Fact]
        public void ParseVerificationKey_EmptyJson_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => SnarkjsVerificationKeyParser.Parse(""));
        }

        [Fact]
        public void ParsePublicInputs_EmptyJson_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => SnarkjsPublicInputParser.Parse(""));
        }
    }
}
