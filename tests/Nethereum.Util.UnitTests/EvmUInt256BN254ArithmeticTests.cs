using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256BN254ArithmeticTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly BigInteger Prime = BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");
        private static readonly EvmUInt256 EvmPrime = PoseidonPrecomputedConstants.Prime;

        public EvmUInt256BN254ArithmeticTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AddMod_MatchesBigInteger()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            int failures = 0;
            int tested = 0;
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            for (int r = 0; r < totalRounds && r < 5; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var a = parameters.RoundConstants[r, c];
                    var b = parameters.RoundConstants[(r + 1) % totalRounds, c];

                    var biResult = (a + b) % Prime;
                    if (biResult.Sign < 0) biResult += Prime;

                    var evmA = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var evmB = EvmUInt256BigIntegerExtensions.FromBigInteger(b);
                    var evmResult = EvmUInt256.AddMod(evmA, evmB, EvmPrime);
                    var evmBack = EvmUInt256BigIntegerExtensions.ToBigInteger(evmResult);

                    if (biResult != evmBack)
                    {
                        if (failures == 0)
                            _output.WriteLine($"AddMod FAIL at [{r},{c}]: bi={biResult}, evm={evmBack}");
                        failures++;
                    }
                    tested++;
                }
            }
            _output.WriteLine($"AddMod: {tested} tested, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void MulMod_MatchesBigInteger()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            int failures = 0;
            int tested = 0;
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            for (int r = 0; r < totalRounds && r < 5; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var a = parameters.RoundConstants[r, c];
                    var b = parameters.RoundConstants[(r + 1) % totalRounds, c];

                    var biResult = (a * b) % Prime;
                    if (biResult.Sign < 0) biResult += Prime;

                    var evmA = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var evmB = EvmUInt256BigIntegerExtensions.FromBigInteger(b);
                    var evmResult = EvmUInt256.MulMod(evmA, evmB, EvmPrime);
                    var evmBack = EvmUInt256BigIntegerExtensions.ToBigInteger(evmResult);

                    if (biResult != evmBack)
                    {
                        if (failures == 0)
                        {
                            _output.WriteLine($"MulMod FAIL at [{r},{c}]:");
                            _output.WriteLine($"  a={a}");
                            _output.WriteLine($"  b={b}");
                            _output.WriteLine($"  bi result={biResult}");
                            _output.WriteLine($"  evm result={evmBack}");
                        }
                        failures++;
                    }
                    tested++;
                }
            }
            _output.WriteLine($"MulMod: {tested} tested, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void ModPow5_MatchesBigInteger()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            int failures = 0;
            int tested = 0;
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            for (int r = 0; r < totalRounds && r < 5; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var a = parameters.RoundConstants[r, c];
                    var biResult = BigInteger.ModPow(a, 5, Prime);

                    var evmA = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var evmResult = EvmUInt256.ModPow(evmA, (EvmUInt256)5, EvmPrime);
                    var evmBack = EvmUInt256BigIntegerExtensions.ToBigInteger(evmResult);

                    if (biResult != evmBack)
                    {
                        if (failures == 0)
                        {
                            _output.WriteLine($"ModPow5 FAIL at [{r},{c}]:");
                            _output.WriteLine($"  a={a}");
                            _output.WriteLine($"  bi result={biResult}");
                            _output.WriteLine($"  evm result={evmBack}");
                        }
                        failures++;
                    }
                    tested++;
                }
            }
            _output.WriteLine($"ModPow5: {tested} tested, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void ModOperator_MatchesBigInteger_ForSumOfRoundConstants()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            var a_bi = parameters.RoundConstants[0, 0];
            var b_bi = parameters.RoundConstants[1, 0];
            var sum_bi = a_bi + b_bi;

            var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a_bi);
            var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b_bi);
            var sum_evm = a_evm + b_evm;

            var sum_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(sum_evm);
            _output.WriteLine($"a:       {a_bi}");
            _output.WriteLine($"b:       {b_bi}");
            _output.WriteLine($"sum_bi:  {sum_bi}");
            _output.WriteLine($"sum_evm: {sum_evm_bi}");
            Assert.Equal(sum_bi, sum_evm_bi);

            // Also construct from BigInteger directly
            var sum_from_bi = EvmUInt256BigIntegerExtensions.FromBigInteger(sum_bi);
            _output.WriteLine($"sum_evm limbs:     u3={sum_evm.U3:X16} u2={sum_evm.U2:X16} u1={sum_evm.U1:X16} u0={sum_evm.U0:X16}");
            _output.WriteLine($"sum_from_bi limbs: u3={sum_from_bi.U3:X16} u2={sum_from_bi.U2:X16} u1={sum_from_bi.U1:X16} u0={sum_from_bi.U0:X16}");
            _output.WriteLine($"sum_evm == sum_from_bi: {sum_evm == sum_from_bi}");

            var mod_bi = sum_bi % Prime;

            var mod_via_add = sum_evm % EvmPrime;
            var mod_via_frombi = sum_from_bi % EvmPrime;
            var mod_via_add_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(mod_via_add);
            var mod_via_frombi_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(mod_via_frombi);

            _output.WriteLine($"mod_bi:          {mod_bi}");
            _output.WriteLine($"mod_via_add:     {mod_via_add_bi}");
            _output.WriteLine($"mod_via_frombi:  {mod_via_frombi_bi}");
            _output.WriteLine($"sum bits: {sum_bi.GetBitLength()}");

            Assert.Equal(mod_bi, mod_via_frombi_bi);
            Assert.Equal(mod_bi, mod_via_add_bi);
        }

        [Fact]
        public void Divmod_MatchesBigInteger_ForBN254Range()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            int failures = 0;
            int tested = 0;
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            for (int r = 0; r < 10 && r < totalRounds; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var a_bi = parameters.RoundConstants[r, c];
                    var quotient_bi = a_bi / Prime;
                    var remainder_bi = a_bi % Prime;

                    var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a_bi);
                    EvmUInt256.Divmod(a_evm, EvmPrime, out var q_evm, out var r_evm);
                    var q_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(q_evm);
                    var r_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(r_evm);

                    if (quotient_bi != q_evm_bi || remainder_bi != r_evm_bi)
                    {
                        if (failures == 0)
                        {
                            _output.WriteLine($"Divmod FAIL at [{r},{c}]:");
                            _output.WriteLine($"  a={a_bi}");
                            _output.WriteLine($"  bi:  q={quotient_bi} r={remainder_bi}");
                            _output.WriteLine($"  evm: q={q_evm_bi} r={r_evm_bi}");
                        }
                        failures++;
                    }
                    tested++;
                }
            }
            _output.WriteLine($"Divmod: {tested} tested, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Theory]
        [InlineData("32570156787500436625402520277658935199654364467625873075470874862328695123175",
                     "21888242871839275222246405745257275088548364400416034343698204186575808495617")]
        [InlineData("43776485743678550444492811490514550177096728800832068687396408373151616991234",
                     "21888242871839275222246405745257275088548364400416034343698204186575808495617")]
        [InlineData("115792089237316195423570985008687907853269984665640564039457584007913129639935",
                     "21888242871839275222246405745257275088548364400416034343698204186575808495617")]
        [InlineData("57896044618658097711785492504343953926634992332820282019728792003956564819967",
                     "21888242871839275222246405745257275088548364400416034343698204186575808495617")]
        public void Divmod_MatchesBigInteger_VariousLargeValues(string aStr, string bStr)
        {
            var a_bi = BigInteger.Parse(aStr);
            var b_bi = BigInteger.Parse(bStr);

            var q_bi = a_bi / b_bi;
            var r_bi = a_bi % b_bi;

            var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a_bi);
            var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b_bi);
            EvmUInt256.Divmod(a_evm, b_evm, out var q_evm, out var r_evm);

            var q_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(q_evm);
            var r_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(r_evm);

            _output.WriteLine($"[VariousLarge] a: {a_bi} ({a_bi.GetBitLength()} bits)");
            _output.WriteLine($"[VariousLarge] b: {b_bi} ({b_bi.GetBitLength()} bits)");
            _output.WriteLine($"[VariousLarge] bi:  q={q_bi} r={r_bi}");
            _output.WriteLine($"[VariousLarge] evm: q={q_evm_bi} r={r_evm_bi}");
            _output.WriteLine($"[VariousLarge] a limbs: u3={a_evm.U3:X16} u2={a_evm.U2:X16} u1={a_evm.U1:X16} u0={a_evm.U0:X16}");
            _output.WriteLine($"[VariousLarge] r_evm limbs: u3={r_evm.U3:X16} u2={r_evm.U2:X16} u1={r_evm.U1:X16} u0={r_evm.U0:X16}");
            _output.WriteLine($"[VariousLarge] b limbs: u3={b_evm.U3:X16} u2={b_evm.U2:X16} u1={b_evm.U1:X16} u0={b_evm.U0:X16}");
            _output.WriteLine($"[VariousLarge] EvmPrime: u3={EvmPrime.U3:X16} u2={EvmPrime.U2:X16} u1={EvmPrime.U1:X16} u0={EvmPrime.U0:X16}");
            _output.WriteLine($"[VariousLarge] b == EvmPrime: {b_evm == EvmPrime}");
            _output.WriteLine($"[VariousLarge] match: q={q_bi == q_evm_bi} r={r_bi == r_evm_bi}");

            Assert.Equal(q_bi, q_evm_bi);
            Assert.Equal(r_bi, r_evm_bi);
        }

        [Fact]
        public void Limbs_FromParse_MatchHardcoded()
        {
            var from_parse = EvmUInt256BigIntegerExtensions.FromBigInteger(
                BigInteger.Parse("32570156787500436625402520277658935199654364467625873075470874862328695123175"));
            var hardcoded = new EvmUInt256(0x48020E32D9BA8E04UL, 0xA8AD77FE1D287E34UL, 0x0DD14C04B80BB212UL, 0x89216D6BD4B66CE7UL);

            _output.WriteLine($"parse:    u3={from_parse.U3:X16} u2={from_parse.U2:X16} u1={from_parse.U1:X16} u0={from_parse.U0:X16}");
            _output.WriteLine($"hardcode: u3={hardcoded.U3:X16} u2={hardcoded.U2:X16} u1={hardcoded.U1:X16} u0={hardcoded.U0:X16}");
            _output.WriteLine($"equal: {from_parse == hardcoded}");

            // Now test divmod on both
            EvmUInt256.Divmod(from_parse, EvmPrime, out var q1, out var r1);
            EvmUInt256.Divmod(hardcoded, EvmPrime, out var q2, out var r2);

            _output.WriteLine($"r1: {EvmUInt256BigIntegerExtensions.ToBigInteger(r1)}");
            _output.WriteLine($"r2: {EvmUInt256BigIntegerExtensions.ToBigInteger(r2)}");

            Assert.Equal(from_parse, hardcoded);
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void Divmod_ExactLimbs_Reproducer()
        {
            // Exact limbs from the failing ModOperator test
            var a = new EvmUInt256(0x48020E32D9BA8E04UL, 0xA8AD77FE1D287E34UL, 0x0DD14C04B80BB212UL, 0x89216D6BD4B66CE7UL);
            var b = EvmPrime;

            EvmUInt256.Divmod(a, b, out var q, out var r);

            var a_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(a);
            var q_bi_expected = a_bi / Prime;
            var r_bi_expected = a_bi % Prime;

            var q_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(q);
            var r_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(r);

            _output.WriteLine($"a:         {a_bi}");
            _output.WriteLine($"expected:  q={q_bi_expected} r={r_bi_expected}");
            _output.WriteLine($"got:       q={q_bi} r={r_bi}");
            _output.WriteLine($"q match:   {q_bi_expected == q_bi}");
            _output.WriteLine($"r match:   {r_bi_expected == r_bi}");

            Assert.Equal(q_bi_expected, q_bi);
            Assert.Equal(r_bi_expected, r_bi);
        }

        [Fact]
        public void Divmod_MatchesBigInteger_ForSumGreaterThanPrime()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            var a_bi = parameters.RoundConstants[0, 0];
            var b_bi = parameters.RoundConstants[1, 0];
            var sum_bi = a_bi + b_bi;

            Assert.True(sum_bi > Prime, "Sum should be > prime for this test");

            var sum_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(sum_bi);

            var q_bi = sum_bi / Prime;
            var r_bi = sum_bi % Prime;

            EvmUInt256.Divmod(sum_evm, EvmPrime, out var q_evm, out var r_evm);
            var q_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(q_evm);
            var r_evm_bi = EvmUInt256BigIntegerExtensions.ToBigInteger(r_evm);

            _output.WriteLine($"sum:   {sum_bi} ({sum_bi.GetBitLength()} bits)");
            _output.WriteLine($"prime: {Prime} ({Prime.GetBitLength()} bits)");
            _output.WriteLine($"bi:  q={q_bi} r={r_bi}");
            _output.WriteLine($"evm: q={q_evm_bi} r={r_evm_bi}");

            Assert.Equal(q_bi, q_evm_bi);
            Assert.Equal(r_bi, r_evm_bi);
        }

        [Fact]
        public void FromBytes_MatchesBigInteger()
        {
            var inputs = new byte[][]
            {
                new byte[] { 0x01, 0x02, 0x03, 0x04 },
                new byte[] { 0xFF, 0xFE, 0xFD, 0xFC },
                new byte[32],
                new byte[] { 0x30, 0x64, 0x4e, 0x72 },
            };

            int failures = 0;
            foreach (var input in inputs)
            {
                var bi = input.ToBigIntegerFromUnsignedBigEndian() % Prime;
                if (bi.Sign < 0) bi += Prime;

                var padded = input.Length < 32 ? ByteUtil.PadBytes(input, 32) : input;
                var evm = EvmUInt256.FromBigEndian(padded) % EvmPrime;
                var evmBack = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);

                if (bi != evmBack)
                {
                    _output.WriteLine($"FromBytes FAIL: bi={bi}, evm={evmBack}");
                    failures++;
                }
            }
            _output.WriteLine($"FromBytes: {inputs.Length} tested, {failures} failures");
            Assert.Equal(0, failures);
        }
    }
}
