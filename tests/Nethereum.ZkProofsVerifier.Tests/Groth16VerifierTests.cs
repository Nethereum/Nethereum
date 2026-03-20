using System;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Groth16;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests
{
    public class Groth16VerifierTests
    {
        [Fact]
        public void Verify_NullProof_ReturnsInvalid()
        {
            var verifier = new Groth16Verifier();
            var result = verifier.Verify(null, new Groth16VerificationKey(), new BigInteger[0]);
            Assert.False(result.IsValid);
            Assert.Contains("null", result.Error);
        }

        [Fact]
        public void Verify_NullVk_ReturnsInvalid()
        {
            var verifier = new Groth16Verifier();
            var result = verifier.Verify(new Groth16Proof(), null, new BigInteger[0]);
            Assert.False(result.IsValid);
            Assert.Contains("null", result.Error);
        }

        [Fact]
        public void Verify_NullPublicInputs_ReturnsInvalid()
        {
            var verifier = new Groth16Verifier();
            var result = verifier.Verify(new Groth16Proof(), new Groth16VerificationKey(), null);
            Assert.False(result.IsValid);
            Assert.Contains("null", result.Error);
        }

        [Fact]
        public void Verify_EmptyIC_ReturnsInvalid()
        {
            var verifier = new Groth16Verifier();
            var vk = new Groth16VerificationKey { IC = new Org.BouncyCastle.Math.EC.ECPoint[0] };
            var result = verifier.Verify(new Groth16Proof(), vk, new BigInteger[0]);
            Assert.False(result.IsValid);
            Assert.Contains("empty", result.Error);
        }

        [Fact]
        public void Verify_MismatchedPublicInputCount_ReturnsInvalid()
        {
            var verifier = new Groth16Verifier();
            var vk = new Groth16VerificationKey
            {
                IC = new[] { BN128Curve.Curve.Infinity, BN128Curve.Curve.Infinity }
            };
            var result = verifier.Verify(new Groth16Proof(), vk, new BigInteger[] { BigInteger.One, BigInteger.Two });
            Assert.False(result.IsValid);
            Assert.Contains("Expected 1", result.Error);
        }
    }
}
