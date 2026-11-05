using System;
using System.IO;
using System.Text.RegularExpressions;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12
{
    public class Groth16SoundnessTests
    {
        private static readonly BigInteger ScalarField =
            new BigInteger("21888242871839275222246405745257275088548364400416034343698204186575808495617");
        private static readonly BigInteger BaseField =
            new BigInteger("21888242871839275222246405745257275088696311157297823662689037894645226208583");

        private static string Read(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", f));

        [Fact]
        [Trait("Rule", "S12-1")]
        public void Given_a_valid_proof_when_a_public_input_is_offset_by_the_scalar_field_order_then_it_is_rejected()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            Assert.True(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);

            inputs[0] = inputs[0].Add(ScalarField);

            Assert.False(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-1")]
        public void Given_a_valid_proof_when_a_public_input_is_negative_equivalent_then_it_is_rejected()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));

            inputs[1] = inputs[1].Subtract(ScalarField);

            Assert.False(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-1")]
        public void Given_a_valid_proof_when_public_inputs_json_carries_an_unreduced_value_then_the_adapter_rejects_it()
        {
            var unreduced = new BigInteger("33").Add(ScalarField).ToString();
            var json = "[\"" + unreduced + "\",\"3\",\"11\"]";

            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json);

            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-2")]
        public void Given_a_valid_proof_when_pi_a_x_is_offset_by_the_base_field_order_then_the_parser_or_verifier_rejects_it()
        {
            var json = Read("proof.json");
            var m = Regex.Match(json, @"""pi_a""\s*:\s*\[\s*""(\d+)""");
            Assert.True(m.Success);
            var x = new BigInteger(m.Groups[1].Value).Add(BaseField).ToString();
            var forged = json.Substring(0, m.Groups[1].Index) + x + json.Substring(m.Groups[1].Index + m.Groups[1].Length);

            var accepted = false;
            try { accepted = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json")).IsValid; }
            catch (Exception) { accepted = false; }

            Assert.False(accepted);
        }

        [Fact]
        [Trait("Rule", "S12-3")]
        public void Given_a_proof_json_when_pi_a_is_not_on_the_curve_then_parsing_rejects_it()
        {
            var json = Read("proof.json");
            var m = Regex.Match(json, @"""pi_a""\s*:\s*\[\s*""\d+""\s*,\s*""(\d+)""");
            var y = new BigInteger(m.Groups[1].Value).Add(BigInteger.One).ToString();
            var forged = json.Substring(0, m.Groups[1].Index) + y + json.Substring(m.Groups[1].Index + m.Groups[1].Length);

            Assert.ThrowsAny<Exception>(() => SnarkjsProofParser.Parse(forged));
        }

        [Fact]
        [Trait("Rule", "S12-3")]
        public void Given_a_proof_json_when_pi_b_is_not_on_the_twist_then_parsing_rejects_it()
        {
            var json = Read("proof.json");
            var m = Regex.Match(json, @"""pi_b""\s*:\s*\[\s*\[\s*""\d+""\s*,\s*""\d+""\s*\]\s*,\s*\[\s*""(\d+)""");
            var y = new BigInteger(m.Groups[1].Value).Add(BigInteger.One).ToString();
            var forged = json.Substring(0, m.Groups[1].Index) + y + json.Substring(m.Groups[1].Index + m.Groups[1].Length);

            Assert.ThrowsAny<Exception>(() => SnarkjsProofParser.Parse(forged));
        }

        [Fact]
        [Trait("Rule", "S12-4")]
        public void Given_a_truncated_pi_a_when_verifying_through_the_adapter_then_the_result_is_invalid_not_an_exception()
        {
            var forged = Regex.Replace(Read("proof.json"), @"""pi_a""\s*:\s*\[[^\]]*\]", @"""pi_a"": [""1""]");

            ZkVerificationResultProbe.AssertInvalid(() =>
                CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json")));
        }

        [Fact]
        [Trait("Rule", "S12-4")]
        public void Given_a_proof_with_all_points_at_infinity_when_verified_then_it_is_rejected()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            var inf = new Groth16Proof
            {
                A = proof.A.Curve.Infinity,
                B = Nethereum.Signer.Crypto.BN128.TwistPoint.FromAffine(new Nethereum.Signer.Crypto.BN128.Fp2(), new Nethereum.Signer.Crypto.BN128.Fp2()),
                C = proof.C.Curve.Infinity
            };

            Assert.False(new Groth16Verifier().Verify(inf, vk, inputs).IsValid);
        }
    }

    internal static class ZkVerificationResultProbe
    {
        public static void AssertInvalid(Func<Nethereum.ZkProofsVerifier.Abstractions.ZkVerificationResult> f)
        {
            Nethereum.ZkProofsVerifier.Abstractions.ZkVerificationResult r = null;
            var ex = Record.Exception(() => r = f());
            Assert.Null(ex);
            Assert.False(r.IsValid);
        }
    }
}
