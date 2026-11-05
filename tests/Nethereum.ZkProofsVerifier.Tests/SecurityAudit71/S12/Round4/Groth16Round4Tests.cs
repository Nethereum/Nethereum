using System;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12.Round3;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12.Round4
{
    public class Groth16Round4Tests
    {
        private readonly ITestOutputHelper _output;

        public Groth16Round4Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Read(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", f));

        private static string ArrayOf(int n) => "[" + string.Join(",", Enumerable.Repeat("\"1\"", n)) + "]";

        [Fact]
        [Trait("Rule", "S12-R4-10")]
        public void Given_a_million_entry_public_input_array_then_allocation_is_bounded_by_a_small_multiple_of_the_request()
        {
            var json = ArrayOf(1_000_000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            _output.WriteLine("requestBytes=" + json.Length + " allocated=" + allocated + " ratio=" + (allocated / json.Length));
            Assert.False(result.IsValid);
            Assert.True(allocated < 20L * json.Length, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R4-10")]
        public void Given_a_million_entry_array_through_the_parsed_vk_overload_then_allocation_is_bounded()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var json = ArrayOf(1_000_000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), vk, json);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(result.IsValid);
            Assert.True(allocated < 20L * json.Length, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R4-10")]
        public void Given_the_standalone_parser_then_arrays_over_1024_are_refused_and_1024_are_not()
        {
            Assert.Throws<ArgumentException>(() => SnarkjsPublicInputParser.Parse(ArrayOf(1025)));
            Assert.Equal(1024, SnarkjsPublicInputParser.Parse(ArrayOf(1024)).Length);
        }

        [Fact]
        [Trait("Rule", "S12-R4-11")]
        public void Given_the_right_count_with_one_huge_element_then_allocation_is_linear_and_the_input_is_rejected()
        {
            var huge = new string('9', 5_000_000);
            var json = "[\"" + huge + "\",\"3\",\"11\"]";
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(result.IsValid);
            Assert.True(allocated < 30L * json.Length, "allocated " + allocated);
        }

        [Theory]
        [InlineData("[[\"33\"],[\"3\"],[\"11\"]]")]
        [InlineData("[\"33\",\"3\",\"11\"] trailing")]
        [InlineData("[\"33\",\"3\",\"11\"]]")]
        [InlineData("[\"33\",\"3\",\"11\"],[\"1\"]")]
        [InlineData("[\"33\",\"3\"]")]
        [InlineData("[\"33\",\"3\",\"11\",\"1\"]")]
        [InlineData("[\"33\",\"3\",\"11\"")]
        [InlineData("{\"length\":3}")]
        [InlineData("[\"33\",\"3\",{\"x\":1}]")]
        [Trait("Rule", "S12-R4-11")]
        public void Given_malformed_public_input_documents_then_the_adapter_returns_invalid_without_throwing(string json)
        {
            ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), vk, json));
        }

        [Fact]
        [Trait("Rule", "S12-R4-12")]
        public void Given_deeply_nested_hostile_documents_then_every_adapter_input_is_rejected_without_throwing_or_overflowing()
        {
            var deep = new string('[', 200_000) + new string(']', 200_000);
            var deepObjects = string.Concat(Enumerable.Repeat("{\"a\":", 100_000)) + "1" + new string('}', 100_000);
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));

            foreach (var hostile in new[] { deep, deepObjects })
            {
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), hostile));
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(hostile, Read("verification_key.json"), Read("public.json")));
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), hostile, Read("public.json")));
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(hostile, vk, Read("public.json")));
            }
        }

        [Fact]
        [Trait("Rule", "S12-R4-12")]
        public void Given_a_hostile_document_with_a_huge_number_of_properties_then_allocation_is_linear()
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < 300_000; i++) sb.Append("\"k").Append(i).Append("\":1,");
            sb.Append("\"pi_a\":[\"1\",\"2\",\"1\"]}");
            var json = sb.ToString();

            var before = GC.GetAllocatedBytesForCurrentThread();
            ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(json, Read("verification_key.json"), Read("public.json")));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 40L * json.Length, "allocated " + allocated);
        }

        [Theory]
        [InlineData("")]
        [InlineData("square/")]
        [InlineData("threeinputs/")]
        [Trait("Rule", "S12-R4-20")]
        public void Given_each_shipped_fixture_then_the_direct_verifier_accepts_the_valid_proof(string dir)
        {
            var proof = SnarkjsProofParser.Parse(Read(dir + "proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read(dir + "verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read(dir + "public.json"));

            Assert.True(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);
            Assert.True(CircomGroth16Adapter.Verify(Read(dir + "proof.json"), Read(dir + "verification_key.json"), Read(dir + "public.json")).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R4-21")]
        public void Given_a_proof_with_a_point_at_infinity_on_the_direct_path_then_it_reaches_the_pairing_and_is_rejected()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            var verifier = new Groth16Verifier();

            var cases = new[]
            {
                new Groth16Proof { A = BN128Curve.Curve.Infinity, B = proof.B, C = proof.C },
                new Groth16Proof { A = proof.A, B = proof.B, C = BN128Curve.Curve.Infinity },
                new Groth16Proof { A = proof.A, B = TwistPoint.FromAffine(new Fp2(), new Fp2()), C = proof.C },
                new Groth16Proof { A = BN128Curve.Curve.Infinity, B = TwistPoint.FromAffine(new Fp2(), new Fp2()), C = BN128Curve.Curve.Infinity }
            };

            foreach (var c in cases)
            {
                var r = verifier.Verify(c, vk, inputs);
                Assert.False(r.IsValid);
                Assert.Equal("Pairing check failed", r.Error);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R4-22")]
        public void Given_null_or_invalid_points_on_the_direct_path_then_the_verifier_returns_invalid_without_throwing()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            var offCurve = BN128Curve.Curve.CreatePoint(new Org.BouncyCastle.Math.BigInteger("1"), new Org.BouncyCastle.Math.BigInteger("1"));

            var cases = new[]
            {
                new Groth16Proof { A = null, B = proof.B, C = proof.C },
                new Groth16Proof { A = proof.A, B = null, C = proof.C },
                new Groth16Proof { A = proof.A, B = proof.B, C = null },
                new Groth16Proof { A = offCurve, B = proof.B, C = proof.C },
                new Groth16Proof { A = proof.A, B = proof.B, C = offCurve }
            };

            foreach (var c in cases)
                ZkVerificationProbe.AssertInvalid(() => new Groth16Verifier().Verify(c, vk, inputs));
        }
    }
}
