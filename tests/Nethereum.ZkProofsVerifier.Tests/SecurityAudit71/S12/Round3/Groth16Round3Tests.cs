using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Xunit;
using Xunit.Abstractions;
using BcInt = Org.BouncyCastle.Math.BigInteger;
using NumInt = System.Numerics.BigInteger;

namespace Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12.Round3
{
    public class Groth16Round3Tests
    {
        private static readonly NumInt P = NumInt.Parse("21888242871839275222246405745257275088696311157297823662689037894645226208583");
        private static readonly BcInt Order = new BcInt("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        private readonly ITestOutputHelper _output;

        public Groth16Round3Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Read(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", f));

        private static NumInt Mod(NumInt v) => ((v % P) + P) % P;

        private static NumInt Pow(NumInt b, NumInt e) => NumInt.ModPow(b, e, P);

        private static NumInt Inv(NumInt v) => Pow(Mod(v), P - 2);

        private static (NumInt re, NumInt im) Mul((NumInt re, NumInt im) a, (NumInt re, NumInt im) b)
            => (Mod(a.re * b.re - a.im * b.im), Mod(a.re * b.im + a.im * b.re));

        private static bool TrySqrtFp(NumInt v, out NumInt root)
        {
            v = Mod(v);
            root = Pow(v, (P + 1) / 4);
            return Mod(root * root) == v;
        }

        private static bool TrySqrtFp2((NumInt re, NumInt im) v, out (NumInt re, NumInt im) root)
        {
            root = (0, 0);
            if (v.im == 0)
            {
                if (TrySqrtFp(v.re, out var r0)) { root = (r0, 0); return true; }
                if (TrySqrtFp(Mod(-v.re), out var r1)) { root = (0, r1); return true; }
                return false;
            }
            var norm = Mod(v.re * v.re + v.im * v.im);
            if (!TrySqrtFp(norm, out var s)) return false;
            var inv2 = Inv(2);
            foreach (var cand in new[] { Mod((v.re + s) * inv2), Mod((v.re - s) * inv2) })
            {
                if (!TrySqrtFp(cand, out var x0) || x0 == 0) continue;
                var x1 = Mod(v.im * Inv(2 * x0));
                var check = Mul((x0, x1), (x0, x1));
                if (check.re == Mod(v.re) && check.im == Mod(v.im)) { root = (x0, x1); return true; }
            }
            return false;
        }

        private static BcInt ToBc(NumInt v) => new BcInt(v.ToString());

        private static TwistPoint FindTwistPointOutsideSubgroup()
        {
            var twistB = (Mod(27 * Inv(82)), Mod(-3 * Inv(82)));
            for (int seed = 1; seed < 500; seed++)
            {
                var x = ((NumInt)seed, (NumInt)(seed + 7));
                var rhs = Mul(Mul(x, x), x);
                rhs = (Mod(rhs.re + twistB.Item1), Mod(rhs.im + twistB.Item2));
                if (TrySqrtFp2(rhs, out var y))
                {
                    var point = TwistPoint.FromAffine(new Fp2(ToBc(x.Item2), ToBc(x.Item1)), new Fp2(ToBc(y.im), ToBc(y.re)));
                    if (!point.IsInCorrectSubgroup())
                        return point;
                }
            }
            throw new InvalidOperationException("no twist point found");
        }

        private static string ReplacePublic(string[] values) => "[" + string.Join(",", values.Select(v => "\"" + v + "\"")) + "]";

        [Fact]
        [Trait("Rule", "S12-R3-1")]
        public void Given_a_valid_proof_when_b_is_shifted_by_a_cofactor_torsion_point_then_the_direct_verifier_rejects_it()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            Assert.True(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);

            var q = FindTwistPointOutsideSubgroup();
            var torsion = new TwistPoint().ScalarMul(q, Order);
            Assert.False(torsion.IsInfinity());
            Assert.False(torsion.IsInCorrectSubgroup());

            var shifted = new TwistPoint().Add(proof.B, torsion);
            var forged = new Groth16Proof { A = proof.A, B = shifted, C = proof.C };

            Assert.False(new Groth16Verifier().Verify(forged, vk, inputs).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R3-1")]
        public void Given_a_valid_proof_when_b_is_shifted_by_a_cofactor_torsion_point_then_the_adapter_rejects_it()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var q = FindTwistPointOutsideSubgroup();
            var torsion = new TwistPoint().ScalarMul(q, Order);
            var shifted = new TwistPoint().Add(proof.B, torsion).Copy();
            shifted.MakeAffine();

            var json = Read("proof.json");
            var replacement = "\"pi_b\": [[\"" + shifted.X.B + "\",\"" + shifted.X.A + "\"],[\"" + shifted.Y.B + "\",\"" + shifted.Y.A + "\"],[\"1\",\"0\"]]";
            var forged = Regex.Replace(json, "\"pi_b\"\\s*:\\s*\\[\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*\\]", replacement);

            var parseEx = Assert.ThrowsAny<Exception>(() => SnarkjsProofParser.Parse(forged));
            Assert.Contains("subgroup", parseEx.Message);
            Assert.False(CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json")).IsValid);
        }

        [Theory]
        [InlineData("\u0663\u0663")]
        [InlineData("\uFF13\uFF13")]
        [InlineData("33 ")]
        [InlineData(" 33")]
        [InlineData("+33")]
        [InlineData("-33")]
        [InlineData("3.3e1")]
        [InlineData("0x21")]
        [InlineData("")]
        [InlineData("1\u0000")]
        [Trait("Rule", "S12-R3-2")]
        public void Given_a_public_input_text_that_is_not_ascii_decimal_then_the_adapter_returns_invalid(string text)
        {
            var json = ReplacePublic(new[] { text, "3", "11" });
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json);
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R3-2")]
        public void Given_a_public_input_of_79_digits_then_it_is_rejected_before_any_parse_and_78_digits_are_still_parsed()
        {
            var seventyEight = new string('0', 77) + "1";
            var ok = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), ReplacePublic(new[] { seventyEight, "3", "11" }));
            Assert.DoesNotContain("1 to 78", ok.Error ?? "");

            var seventyNine = new string('0', 78) + "1";
            var bad = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), ReplacePublic(new[] { seventyNine, "3", "11" }));
            Assert.False(bad.IsValid);
            Assert.Contains("1 to 78", bad.Error);
        }

        [Fact]
        [Trait("Rule", "S12-R3-2")]
        public void Given_a_valid_public_input_padded_with_leading_zeros_to_78_digits_then_the_decision_is_recorded()
        {
            var padded = new string('0', 76) + "33";
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), ReplacePublic(new[] { padded, "3", "11" }));
            _output.WriteLine("zero-padded canonical value accepted=" + result.IsValid);
            Assert.True(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R3-3")]
        public void Given_public_inputs_as_json_numbers_or_nested_arrays_then_the_adapter_returns_invalid_without_throwing()
        {
            foreach (var json in new[] { "[33,3,11]", "[[33],[3],[11]]", "{\"a\":1}", "null", "[null,null,null]", "\"33\"", "[" })
            {
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json));
            }
        }

        [Fact]
        [Trait("Rule", "S12-R3-4")]
        public void Given_a_public_input_array_of_one_million_entries_then_the_cost_is_linear_in_the_request_and_reported()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < 1_000_000; i++) { if (i > 0) sb.Append(','); sb.Append("\"1\""); }
            sb.Append(']');
            var json = sb.ToString();

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), json);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            _output.WriteLine("requestBytes=" + json.Length + " allocated=" + allocated + " ratio=" + (allocated / json.Length));
            Assert.False(result.IsValid);
            Assert.True(allocated < 40L * json.Length, "allocated " + allocated + " for " + json.Length);
        }

        [Fact]
        [Trait("Rule", "S12-R3-4")]
        public void Given_a_proof_with_a_huge_extra_array_then_the_cost_is_linear_and_the_proof_is_rejected_or_valid_as_before()
        {
            var json = Read("proof.json");
            var padding = string.Join(",", Enumerable.Repeat("\"1\"", 500_000));
            var forged = Regex.Replace(json, "\"protocol\"", "\"extra\": [" + padding + "], \"protocol\"");

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json"));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            _output.WriteLine("requestBytes=" + forged.Length + " allocated=" + allocated);
            Assert.True(allocated < 40L * forged.Length);
        }

        [Fact]
        [Trait("Rule", "S12-R3-6")]
        public void Given_a_hand_built_verification_key_with_nulls_or_off_curve_points_then_the_overload_returns_invalid_without_throwing()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var offCurve = BN128Curve.Curve.CreatePoint(new BcInt("1"), new BcInt("1"));

            var variants = new[]
            {
                new Groth16VerificationKey { Alpha = null, Beta = vk.Beta, Gamma = vk.Gamma, Delta = vk.Delta, IC = vk.IC },
                new Groth16VerificationKey { Alpha = vk.Alpha, Beta = null, Gamma = vk.Gamma, Delta = vk.Delta, IC = vk.IC },
                new Groth16VerificationKey { Alpha = vk.Alpha, Beta = vk.Beta, Gamma = vk.Gamma, Delta = vk.Delta, IC = new Org.BouncyCastle.Math.EC.ECPoint[] { null, null, null, null } },
                new Groth16VerificationKey { Alpha = offCurve, Beta = vk.Beta, Gamma = vk.Gamma, Delta = vk.Delta, IC = vk.IC },
                new Groth16VerificationKey { Alpha = vk.Alpha, Beta = vk.Beta, Gamma = vk.Gamma, Delta = vk.Delta, IC = vk.IC.Select((p, i) => i == 1 ? offCurve : p).ToArray() },
                new Groth16VerificationKey { Alpha = vk.Alpha, Beta = vk.Beta, Gamma = vk.Gamma, Delta = vk.Delta, IC = new Org.BouncyCastle.Math.EC.ECPoint[0] },
                new Groth16VerificationKey()
            };

            foreach (var variant in variants)
                ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), variant, Read("public.json")));

            ZkVerificationProbe.AssertInvalid(() => CircomGroth16Adapter.Verify(Read("proof.json"), (Groth16VerificationKey)null, Read("public.json")));
        }

        [Fact]
        [Trait("Rule", "S12-R3-7")]
        public void Given_the_overload_with_a_parsed_vk_then_it_matches_the_string_overload_for_valid_and_tampered_inputs()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            Assert.True(CircomGroth16Adapter.Verify(Read("proof.json"), vk, Read("public.json")).IsValid);
            Assert.False(CircomGroth16Adapter.Verify(Read("proof.json"), vk, "[\"34\",\"3\",\"11\"]").IsValid);
            Assert.True(CircomGroth16Adapter.Verify(Read("proof.json"), vk, Read("public.json")).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R3-8")]
        public void Given_one_parsed_vk_shared_across_threads_then_valid_and_invalid_verdicts_never_mix()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var proof = Read("proof.json");
            var good = Read("public.json");
            var bad = "[\"34\",\"3\",\"11\"]";

            var results = new bool[48];
            Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 12 }, i =>
            {
                var r = CircomGroth16Adapter.Verify(proof, vk, i % 2 == 0 ? good : bad);
                results[i] = r.IsValid == (i % 2 == 0);
            });

            Assert.All(results, Assert.True);
        }
    }

    internal static class ZkVerificationProbe
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
