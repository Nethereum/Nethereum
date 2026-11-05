using System;
using System.IO;
using System.Text.RegularExpressions;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Xunit;
using Xunit.Abstractions;
using BcInt = Org.BouncyCastle.Math.BigInteger;
using NumInt = System.Numerics.BigInteger;

namespace Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12.Round2
{
    public class Groth16Round2Tests
    {
        private static readonly NumInt P = NumInt.Parse("21888242871839275222246405745257275088696311157297823662689037894645226208583");
        private static readonly NumInt R = NumInt.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        private readonly ITestOutputHelper _output;

        public Groth16Round2Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Read(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", f));

        private static NumInt Mod(NumInt v) => ((v % P) + P) % P;

        private static NumInt Pow(NumInt b, NumInt e) => NumInt.ModPow(b, e, P);

        private static NumInt Inv(NumInt v) => Pow(Mod(v), P - 2);

        private static (NumInt re, NumInt im) Mul((NumInt re, NumInt im) a, (NumInt re, NumInt im) b)
            => (Mod(a.re * b.re - a.im * b.im), Mod(a.re * b.im + a.im * b.re));

        private static (NumInt re, NumInt im) Add((NumInt re, NumInt im) a, (NumInt re, NumInt im) b)
            => (Mod(a.re + b.re), Mod(a.im + b.im));

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

        private static ((NumInt re, NumInt im) x, (NumInt re, NumInt im) y) FindTwistPointOutsideSubgroup()
        {
            var twistB = (Mod(27 * Inv(82)), Mod(-3 * Inv(82)));
            for (int seed = 1; seed < 500; seed++)
            {
                var x = ((NumInt)seed, (NumInt)(seed + 7));
                var x3 = Mul(Mul(x, x), x);
                var rhs = Add(x3, twistB);
                if (TrySqrtFp2(rhs, out var y))
                {
                    var point = TwistPoint.FromAffine(new Fp2(ToBc(x.Item2), ToBc(x.Item1)), new Fp2(ToBc(y.im), ToBc(y.re)));
                    if (!point.IsInCorrectSubgroup())
                        return (x, y);
                }
            }
            throw new InvalidOperationException("no twist point found");
        }

        private static BcInt ToBc(NumInt v) => new BcInt(v.ToString());

        private static string ProofWithPiB(string json, ((NumInt re, NumInt im) x, (NumInt re, NumInt im) y) b)
        {
            var replacement = "\"pi_b\": [[\"" + b.x.re + "\",\"" + b.x.im + "\"],[\"" + b.y.re + "\",\"" + b.y.im + "\"],[\"1\",\"0\"]]";
            return Regex.Replace(json, "\"pi_b\"\\s*:\\s*\\[\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*\\]", replacement);
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_valid_proof_when_pi_b_is_on_the_twist_but_outside_the_r_subgroup_then_the_adapter_rejects_it_as_a_subgroup_failure()
        {
            var outside = FindTwistPointOutsideSubgroup();
            var forged = ProofWithPiB(Read("proof.json"), outside);

            var parseEx = Assert.ThrowsAny<Exception>(() => SnarkjsProofParser.Parse(forged));
            Assert.Contains("subgroup", parseEx.Message);

            var result = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json"));
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_verification_key_when_delta_is_on_the_twist_but_outside_the_r_subgroup_then_the_vk_parser_rejects_it()
        {
            var outside = FindTwistPointOutsideSubgroup();
            var vk = Read("verification_key.json");
            var replacement = "\"vk_delta_2\": [[\"" + outside.x.re + "\",\"" + outside.x.im + "\"],[\"" + outside.y.re + "\",\"" + outside.y.im + "\"],[\"1\",\"0\"]]";
            var forged = Regex.Replace(vk, "\"vk_delta_2\"\\s*:\\s*\\[\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*,\\s*\\[[^\\]]*\\]\\s*\\]", replacement);
            Assert.NotEqual(vk, forged);

            Assert.ThrowsAny<Exception>(() => SnarkjsVerificationKeyParser.Parse(forged));
            Assert.False(CircomGroth16Adapter.Verify(Read("proof.json"), forged, Read("public.json")).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-2")]
        public void Given_a_non_subgroup_g2_point_set_directly_on_a_proof_then_the_verifier_returns_invalid_without_throwing()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inputs = SnarkjsPublicInputParser.Parse(Read("public.json"));
            var outside = FindTwistPointOutsideSubgroup();
            proof.B = TwistPoint.FromAffine(new Fp2(ToBc(outside.x.im), ToBc(outside.x.re)), new Fp2(ToBc(outside.y.im), ToBc(outside.y.re)));

            var result = new Groth16Verifier().Verify(proof, vk, inputs);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("c")]
        [Trait("Rule", "S12-R2-3")]
        public void Given_a_valid_proof_when_a_g1_point_is_the_zero_zero_infinity_encoding_then_it_is_rejected(string which)
        {
            var json = Read("proof.json");
            var field = which == "a" ? "pi_a" : "pi_c";
            var forged = Regex.Replace(json, "\"" + field + "\"\\s*:\\s*\\[[^\\]]*\\]", "\"" + field + "\": [\"0\",\"0\",\"1\"]");
            Assert.NotEqual(json, forged);

            var result = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json"));
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-3")]
        public void Given_a_valid_proof_when_pi_b_is_infinity_then_it_is_rejected()
        {
            var forged = ProofWithPiB(Read("proof.json"), ((0, 0), (0, 0)));
            var result = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json"));
            Assert.False(result.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-3")]
        public void Given_a_proof_of_all_infinity_points_when_every_public_input_is_tried_around_zero_then_none_is_accepted()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var inf = new Groth16Proof
            {
                A = BN128Curve.Curve.Infinity,
                B = TwistPoint.FromAffine(new Fp2(), new Fp2()),
                C = BN128Curve.Curve.Infinity
            };
            var verifier = new Groth16Verifier();
            for (int a = 0; a < 6; a++)
                for (int b = 0; b < 6; b++)
                {
                    var inputs = new[] { new BcInt(a.ToString()), new BcInt(b.ToString()), BcInt.Zero };
                    Assert.False(verifier.Verify(inf, vk, inputs).IsValid);
                }
        }

        [Fact]
        [Trait("Rule", "S12-R2-4")]
        public void Given_random_g1_curve_points_then_multiplying_by_the_group_order_gives_infinity_confirming_cofactor_one()
        {
            int tested = 0;
            for (int x = 2; tested < 12 && x < 400; x++)
            {
                var rhs = Mod(NumInt.Pow(x, 3) + 3);
                if (!TrySqrtFp(rhs, out var y)) continue;
                var point = BN128Curve.Curve.CreatePoint(ToBc(x), ToBc(y));
                Assert.True(point.IsValid());
                Assert.True(point.Multiply(ToBc(R)).IsInfinity);
                tested++;
            }
            Assert.Equal(12, tested);
        }

        [Fact]
        [Trait("Rule", "S12-R2-5")]
        public void Given_non_canonical_coordinates_then_the_parser_rejects_them()
        {
            var json = Read("proof.json");
            var m = Regex.Match(json, "\"pi_c\"\\s*:\\s*\\[\\s*\"(\\d+)\"");
            var x = NumInt.Parse(m.Groups[1].Value);
            foreach (var bad in new[] { (x + P).ToString(), "-" + x, "+" + x + "x" })
            {
                var forged = json.Substring(0, m.Groups[1].Index) + bad + json.Substring(m.Groups[1].Index + m.Groups[1].Length);
                Assert.False(CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json")).IsValid);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-6")]
        public void Given_the_scalar_field_boundary_then_the_verifier_accepts_r_minus_one_as_input_shape_and_rejects_r()
        {
            var proof = SnarkjsProofParser.Parse(Read("proof.json"));
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var verifier = new Groth16Verifier();

            var atLimit = verifier.Verify(proof, vk, new[] { ToBc(R - 1), BcInt.One, BcInt.One });
            Assert.DoesNotContain("canonical", atLimit.Error ?? "");

            var over = verifier.Verify(proof, vk, new[] { ToBc(R), BcInt.One, BcInt.One });
            Assert.False(over.IsValid);
            Assert.Contains("canonical", over.Error);

            var withNull = verifier.Verify(proof, vk, new BcInt[] { null, BcInt.One, BcInt.One });
            Assert.False(withNull.IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-7")]
        public void Given_a_huge_public_input_string_then_the_adapter_rejects_it_with_bounded_allocation()
        {
            var huge = new string('9', 200000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(Read("proof.json"), Read("verification_key.json"), "[\"" + huge + "\",\"3\",\"11\"]");
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(result.IsValid);
            _output.WriteLine("allocated=" + allocated);
            Assert.True(allocated < 64L * 1024 * 1024, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R2-7")]
        public void Given_a_huge_coordinate_string_in_the_proof_then_the_adapter_rejects_it_with_bounded_allocation()
        {
            var json = Read("proof.json");
            var m = Regex.Match(json, "\"pi_c\"\\s*:\\s*\\[\\s*\"(\\d+)\"");
            var forged = json.Substring(0, m.Groups[1].Index) + new string('9', 200000) + json.Substring(m.Groups[1].Index + m.Groups[1].Length);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = CircomGroth16Adapter.Verify(forged, Read("verification_key.json"), Read("public.json"));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(result.IsValid);
            _output.WriteLine("allocated=" + allocated);
            Assert.True(allocated < 64L * 1024 * 1024, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R2-8")]
        public void Given_the_measurement_of_parse_and_verify_cost_then_it_is_reported()
        {
            var vkJson = Read("verification_key.json");
            var proofJson = Read("proof.json");
            var pub = Read("public.json");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) SnarkjsVerificationKeyParser.Parse(vkJson);
            var vkMs = sw.Elapsed.TotalMilliseconds / 10;
            sw.Restart();
            for (int i = 0; i < 10; i++) SnarkjsProofParser.Parse(proofJson);
            var proofMs = sw.Elapsed.TotalMilliseconds / 10;
            sw.Restart();
            for (int i = 0; i < 10; i++) Assert.True(CircomGroth16Adapter.Verify(proofJson, vkJson, pub).IsValid);
            var totalMs = sw.Elapsed.TotalMilliseconds / 10;
            _output.WriteLine("vkParseMs=" + vkMs + " proofParseMs=" + proofMs + " fullVerifyMs=" + totalMs);
        }
    }
}
