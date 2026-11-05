using System;
using Nethereum.ZkProofs;
using Nethereum.ZkProofs.Groth16;
using Xunit;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S12
{
    public class FieldElementBoundTests
    {
        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_huge_public_signal_when_parsed_then_it_is_rejected_with_bounded_allocation()
        {
            var json = "[\"" + new string('9', 200000) + "\"]";
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => ZkProofResult.ParsePublicSignals(json));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 16L * 1024 * 1024);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("+1")]
        [InlineData("0x10")]
        [InlineData(" 1")]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_public_signal_that_is_not_plain_decimal_digits_then_it_is_rejected(string text)
        {
            Assert.ThrowsAny<Exception>(() => ZkProofResult.ParsePublicSignals("[\"" + text + "\"]"));
        }

        [Fact]
        [Trait("Rule", "S12-R3-1")]
        public void Given_more_than_1024_public_signals_then_parsing_is_rejected_with_bounded_allocation()
        {
            Assert.Equal(1024, ZkProofResult.ParsePublicSignals("[" + string.Join(",", System.Linq.Enumerable.Repeat("\"1\"", 1024)) + "]").Length);
            var json = "[" + string.Join(",", System.Linq.Enumerable.Repeat("\"1\"", 1_000_000)) + "]";
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => ZkProofResult.ParsePublicSignals(json));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 40L * json.Length);
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_valid_public_signals_then_they_still_parse()
        {
            var signals = ZkProofResult.ParsePublicSignals("[\"0\",\"123\",\"21888242871839275222246405745257275088548364400416034343698204186575808495616\"]");
            Assert.Equal(3, signals.Length);
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_huge_proof_coordinate_when_converting_to_a_solidity_proof_then_it_is_rejected_with_bounded_allocation()
        {
            var huge = new string('9', 200000);
            var proof = new Groth16ProofJson
            {
                PiA = new[] { huge, "1", "1" },
                PiB = new[] { new[] { "1", "1" }, new[] { "1", "1" }, new[] { "1", "0" } },
                PiC = new[] { "1", "1", "1" }
            };
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => Groth16ProofConverter.ToSolidityProof(proof));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 16L * 1024 * 1024);
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_huge_public_signal_when_the_proof_verifier_is_used_then_it_returns_invalid_with_bounded_allocation()
        {
            var verifier = new PrivacyPoolProofVerifier("not a verification key");
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = verifier.VerifyWithdrawalProof("{}", "[\"" + new string('9', 200000) + "\"]");
            Assert.False(result.IsValid);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 16L * 1024 * 1024);
        }
    }
}
