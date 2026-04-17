using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256ComprehensiveTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly System.Random Rng = new System.Random(42);

        private static readonly BigInteger MAX_UINT256 = (BigInteger.One << 256) - 1;
        private static readonly BigInteger TWO_256 = BigInteger.One << 256;
        private static readonly BigInteger BN254_PRIME = BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        private static readonly BigInteger[] BOUNDARY_VALUES = new[]
        {
            BigInteger.Zero,
            BigInteger.One,
            new BigInteger(2),
            new BigInteger(255),
            new BigInteger(256),
            (BigInteger.One << 32) - 1,
            BigInteger.One << 32,
            (BigInteger.One << 64) - 1,
            BigInteger.One << 64,
            (BigInteger.One << 128) - 1,
            BigInteger.One << 128,
            (BigInteger.One << 192) - 1,
            BigInteger.One << 192,
            (BigInteger.One << 254) - 1,
            (BigInteger.One << 255) - 1,
            BigInteger.One << 255,
            MAX_UINT256,
            MAX_UINT256 - 1,
            BN254_PRIME,
            BN254_PRIME - 1,
            BN254_PRIME + 1,
        };

        public EvmUInt256ComprehensiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // --- Addition ---

        [Fact]
        public void Add_Boundaries()
        {
            int failures = RunBinaryOp("Add", BOUNDARY_VALUES,
                (a, b) => (a + b) & MAX_UINT256,
                (a, b) => a + b);
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Add_Fuzz()
        {
            int failures = RunFuzzBinaryOp("Add", 1000,
                (a, b) => (a + b) & MAX_UINT256,
                (a, b) => a + b);
            Assert.Equal(0, failures);
        }

        // --- Subtraction ---

        [Fact]
        public void Sub_Boundaries()
        {
            int failures = RunBinaryOp("Sub", BOUNDARY_VALUES,
                (a, b) => ((a - b) + TWO_256) & MAX_UINT256,
                (a, b) => a - b);
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Sub_Fuzz()
        {
            int failures = RunFuzzBinaryOp("Sub", 1000,
                (a, b) => ((a - b) + TWO_256) & MAX_UINT256,
                (a, b) => a - b);
            Assert.Equal(0, failures);
        }

        // --- Multiplication ---

        [Fact]
        public void Mul_Boundaries()
        {
            int failures = RunBinaryOp("Mul", BOUNDARY_VALUES,
                (a, b) => (a * b) & MAX_UINT256,
                (a, b) => a * b);
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Mul_Fuzz()
        {
            int failures = RunFuzzBinaryOp("Mul", 1000,
                (a, b) => (a * b) & MAX_UINT256,
                (a, b) => a * b);
            Assert.Equal(0, failures);
        }

        // --- Division ---

        [Fact]
        public void Div_Boundaries()
        {
            int failures = RunBinaryOp("Div", BOUNDARY_VALUES,
                (a, b) => b.IsZero ? BigInteger.Zero : a / b,
                (a, b) => b.IsZero ? EvmUInt256.Zero : a / b);
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Div_Fuzz()
        {
            int failures = RunFuzzBinaryOp("Div", 1000,
                (a, b) => b.IsZero ? BigInteger.Zero : a / b,
                (a, b) => b.IsZero ? EvmUInt256.Zero : a / b);
            Assert.Equal(0, failures);
        }

        // --- Modulo ---

        [Fact]
        public void Mod_Boundaries()
        {
            int failures = RunBinaryOp("Mod", BOUNDARY_VALUES,
                (a, b) => b.IsZero ? BigInteger.Zero : a % b,
                (a, b) => b.IsZero ? EvmUInt256.Zero : a % b);
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Mod_Fuzz()
        {
            int failures = RunFuzzBinaryOp("Mod", 1000,
                (a, b) => b.IsZero ? BigInteger.Zero : a % b,
                (a, b) => b.IsZero ? EvmUInt256.Zero : a % b);
            Assert.Equal(0, failures);
        }

        // --- Divmod combined ---

        [Fact]
        public void Divmod_Boundaries()
        {
            int failures = 0;
            foreach (var a_bi in BOUNDARY_VALUES)
            {
                foreach (var b_bi in BOUNDARY_VALUES)
                {
                    if (b_bi.IsZero) continue;
                    var q_bi = a_bi / b_bi;
                    var r_bi = a_bi % b_bi;

                    var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a_bi);
                    var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b_bi);
                    EvmUInt256.Divmod(a_evm, b_evm, out var q_evm, out var r_evm);

                    var q_rt = EvmUInt256BigIntegerExtensions.ToBigInteger(q_evm);
                    var r_rt = EvmUInt256BigIntegerExtensions.ToBigInteger(r_evm);

                    if (q_bi != q_rt || r_bi != r_rt)
                    {
                        if (failures < 3)
                            _output.WriteLine($"Divmod FAIL: a={a_bi}, b={b_bi}, expected q={q_bi} r={r_bi}, got q={q_rt} r={r_rt}");
                        failures++;
                    }
                }
            }
            _output.WriteLine($"Divmod boundaries: {BOUNDARY_VALUES.Length * BOUNDARY_VALUES.Length} pairs, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Divmod_Fuzz()
        {
            int failures = 0;
            for (int i = 0; i < 1000; i++)
            {
                var a_bi = RandomBigInteger();
                var b_bi = RandomBigInteger();
                if (b_bi.IsZero) b_bi = BigInteger.One;

                var q_bi = a_bi / b_bi;
                var r_bi = a_bi % b_bi;

                var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a_bi);
                var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b_bi);
                EvmUInt256.Divmod(a_evm, b_evm, out var q_evm, out var r_evm);

                var q_rt = EvmUInt256BigIntegerExtensions.ToBigInteger(q_evm);
                var r_rt = EvmUInt256BigIntegerExtensions.ToBigInteger(r_evm);

                if (q_bi != q_rt || r_bi != r_rt)
                {
                    if (failures < 3)
                        _output.WriteLine($"Divmod FAIL [{i}]: a bits={a_bi.GetBitLength()}, b bits={b_bi.GetBitLength()}");
                    failures++;
                }
            }
            _output.WriteLine($"Divmod fuzz: 1000 iterations, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- AddMod ---

        [Fact]
        public void AddMod_Boundaries()
        {
            int failures = 0;
            var mods = new[] { BN254_PRIME, BigInteger.One << 128, MAX_UINT256, new BigInteger(7), BigInteger.One << 64 };
            foreach (var m in mods)
            {
                foreach (var a in BOUNDARY_VALUES)
                {
                    foreach (var b in BOUNDARY_VALUES)
                    {
                        var bi = (a + b) % m;
                        var evm = EvmUInt256.AddMod(
                            EvmUInt256BigIntegerExtensions.FromBigInteger(a),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(b),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                        var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                        if (bi != rt)
                        {
                            if (failures < 3)
                                _output.WriteLine($"AddMod FAIL: a={a}, b={b}, m={m}, expected={bi}, got={rt}");
                            failures++;
                        }
                    }
                }
            }
            _output.WriteLine($"AddMod boundaries: {mods.Length * BOUNDARY_VALUES.Length * BOUNDARY_VALUES.Length} triples, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void AddMod_Fuzz()
        {
            int failures = 0;
            for (int i = 0; i < 1000; i++)
            {
                var a = RandomBigInteger();
                var b = RandomBigInteger();
                var m = RandomBigInteger();
                if (m.IsZero) m = BigInteger.One;

                var bi = (a + b) % m;
                var evm = EvmUInt256.AddMod(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(a),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(b),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                if (bi != rt)
                {
                    if (failures < 3)
                        _output.WriteLine($"AddMod FAIL [{i}]: a bits={a.GetBitLength()}, b bits={b.GetBitLength()}, m bits={m.GetBitLength()}");
                    failures++;
                }
            }
            _output.WriteLine($"AddMod fuzz: 1000 iterations, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- MulMod ---

        [Fact]
        public void MulMod_Boundaries()
        {
            int failures = 0;
            var mods = new[] { BN254_PRIME, BigInteger.One << 128, MAX_UINT256, new BigInteger(7) };
            foreach (var m in mods)
            {
                foreach (var a in BOUNDARY_VALUES)
                {
                    foreach (var b in BOUNDARY_VALUES)
                    {
                        var bi = (a * b) % m;
                        var evm = EvmUInt256.MulMod(
                            EvmUInt256BigIntegerExtensions.FromBigInteger(a),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(b),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                        var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                        if (bi != rt)
                        {
                            if (failures < 3)
                                _output.WriteLine($"MulMod FAIL: a={a}, b={b}, m={m}, expected={bi}, got={rt}");
                            failures++;
                        }
                    }
                }
            }
            _output.WriteLine($"MulMod boundaries: {mods.Length * BOUNDARY_VALUES.Length * BOUNDARY_VALUES.Length} triples, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void MulMod_Fuzz()
        {
            int failures = 0;
            for (int i = 0; i < 1000; i++)
            {
                var a = RandomBigInteger();
                var b = RandomBigInteger();
                var m = RandomBigInteger();
                if (m.IsZero) m = BigInteger.One;

                var bi = (a * b) % m;
                var evm = EvmUInt256.MulMod(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(a),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(b),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                if (bi != rt)
                {
                    if (failures < 3)
                        _output.WriteLine($"MulMod FAIL [{i}]: a bits={a.GetBitLength()}, b bits={b.GetBitLength()}, m bits={m.GetBitLength()}");
                    failures++;
                }
            }
            _output.WriteLine($"MulMod fuzz: 1000 iterations, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- ModPow ---

        [Fact]
        public void ModPow_Boundaries()
        {
            int failures = 0;
            var exponents = new BigInteger[] { 0, 1, 2, 3, 5, 7, 255, (BigInteger.One << 64) - 1 };
            var mods = new[] { BN254_PRIME, new BigInteger(997), BigInteger.One << 128, MAX_UINT256 };

            foreach (var m in mods)
            {
                foreach (var a in BOUNDARY_VALUES)
                {
                    foreach (var e in exponents)
                    {
                        if (m.IsZero) continue;
                        var reduced = a % m;
                        var bi = BigInteger.ModPow(reduced, e, m);
                        var evm = EvmUInt256.ModPow(
                            EvmUInt256BigIntegerExtensions.FromBigInteger(reduced),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(e),
                            EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                        var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                        if (bi != rt)
                        {
                            if (failures < 3)
                                _output.WriteLine($"ModPow FAIL: base={a}, exp={e}, mod={m}, expected={bi}, got={rt}");
                            failures++;
                        }
                    }
                }
            }
            _output.WriteLine($"ModPow boundaries: {mods.Length * BOUNDARY_VALUES.Length * exponents.Length} triples, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void ModPow_Fuzz()
        {
            int failures = 0;
            for (int i = 0; i < 500; i++)
            {
                var a = RandomBigInteger();
                var e = RandomBigInteger();
                var m = RandomBigInteger();
                if (m.IsZero) m = BigInteger.One;
                var reduced = a % m;

                var bi = BigInteger.ModPow(reduced, e, m);
                var evm = EvmUInt256.ModPow(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(reduced),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(e),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(m));
                var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                if (bi != rt)
                {
                    if (failures < 3)
                        _output.WriteLine($"ModPow FAIL [{i}]: base bits={a.GetBitLength()}, exp bits={e.GetBitLength()}, mod bits={m.GetBitLength()}");
                    failures++;
                }
            }
            _output.WriteLine($"ModPow fuzz: 500 iterations, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- Shifts ---

        [Fact]
        public void ShiftLeft_Boundaries()
        {
            int failures = 0;
            var shifts = new[] { 0, 1, 7, 8, 31, 32, 33, 63, 64, 65, 127, 128, 191, 192, 255 };
            foreach (var a in BOUNDARY_VALUES)
            {
                foreach (var s in shifts)
                {
                    var bi = (a << s) & MAX_UINT256;
                    var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a) << s;
                    var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                    if (bi != rt)
                    {
                        if (failures < 3)
                            _output.WriteLine($"ShiftLeft FAIL: a={a}, s={s}, expected={bi}, got={rt}");
                        failures++;
                    }
                }
            }
            _output.WriteLine($"ShiftLeft boundaries: {BOUNDARY_VALUES.Length * shifts.Length} pairs, {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void ShiftRight_Boundaries()
        {
            int failures = 0;
            var shifts = new[] { 0, 1, 7, 8, 31, 32, 33, 63, 64, 65, 127, 128, 191, 192, 255 };
            foreach (var a in BOUNDARY_VALUES)
            {
                foreach (var s in shifts)
                {
                    var bi = a >> s;
                    var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a) >> s;
                    var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                    if (bi != rt)
                    {
                        if (failures < 3)
                            _output.WriteLine($"ShiftRight FAIL: a={a}, s={s}, expected={bi}, got={rt}");
                        failures++;
                    }
                }
            }
            _output.WriteLine($"ShiftRight boundaries: {BOUNDARY_VALUES.Length * shifts.Length} pairs, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- Bitwise ---

        [Fact]
        public void Bitwise_And_Or_Xor_Not_Boundaries()
        {
            int failures = 0;
            foreach (var a in BOUNDARY_VALUES)
            {
                foreach (var b in BOUNDARY_VALUES)
                {
                    var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b);

                    Check("AND", a & b, a_evm & b_evm, ref failures);
                    Check("OR", a | b, a_evm | b_evm, ref failures);
                    Check("XOR", a ^ b, a_evm ^ b_evm, ref failures);
                }
                Check("NOT", MAX_UINT256 - a, ~EvmUInt256BigIntegerExtensions.FromBigInteger(a), ref failures);
            }
            _output.WriteLine($"Bitwise boundaries: {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- Comparison ---

        [Fact]
        public void Comparison_Boundaries()
        {
            int failures = 0;
            foreach (var a in BOUNDARY_VALUES)
            {
                foreach (var b in BOUNDARY_VALUES)
                {
                    var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b);

                    if ((a < b) != (a_evm < b_evm)) { failures++; if (failures <= 3) _output.WriteLine($"< FAIL: {a}, {b}"); }
                    if ((a > b) != (a_evm > b_evm)) { failures++; if (failures <= 3) _output.WriteLine($"> FAIL: {a}, {b}"); }
                    if ((a == b) != (a_evm == b_evm)) { failures++; if (failures <= 3) _output.WriteLine($"== FAIL: {a}, {b}"); }
                    if (a.CompareTo(b) != a_evm.CompareTo(b_evm)) { failures++; if (failures <= 3) _output.WriteLine($"CompareTo FAIL: {a}, {b}"); }
                }
            }
            _output.WriteLine($"Comparison boundaries: {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- Conversion round-trip ---

        [Fact]
        public void Conversion_RoundTrip_Boundaries()
        {
            int failures = 0;
            foreach (var a in BOUNDARY_VALUES)
            {
                var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                if (a != rt)
                {
                    _output.WriteLine($"RoundTrip FAIL: {a} → {rt}");
                    failures++;
                }

                var bytes = evm.ToBigEndian();
                var fromBytes = EvmUInt256.FromBigEndian(bytes);
                if (evm != fromBytes)
                {
                    _output.WriteLine($"Bytes RoundTrip FAIL: {a}");
                    failures++;
                }
            }
            _output.WriteLine($"Conversion boundaries: {failures} failures");
            Assert.Equal(0, failures);
        }

        [Fact]
        public void Conversion_RoundTrip_Fuzz()
        {
            int failures = 0;
            for (int i = 0; i < 1000; i++)
            {
                var a = RandomBigInteger();
                var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                if (a != rt) failures++;
            }
            _output.WriteLine($"Conversion fuzz: 1000 iterations, {failures} failures");
            Assert.Equal(0, failures);
        }

        // --- Helpers ---

        private int RunBinaryOp(string name, BigInteger[] values,
            Func<BigInteger, BigInteger, BigInteger> biOp,
            Func<EvmUInt256, EvmUInt256, EvmUInt256> evmOp)
        {
            int failures = 0;
            foreach (var a in values)
            {
                foreach (var b in values)
                {
                    var expected = biOp(a, b);
                    var a_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(a);
                    var b_evm = EvmUInt256BigIntegerExtensions.FromBigInteger(b);
                    var result = evmOp(a_evm, b_evm);
                    var actual = EvmUInt256BigIntegerExtensions.ToBigInteger(result);
                    if (expected != actual)
                    {
                        if (failures < 3)
                            _output.WriteLine($"{name} FAIL: a={a}, b={b}, expected={expected}, got={actual}");
                        failures++;
                    }
                }
            }
            int total = values.Length * values.Length;
            _output.WriteLine($"{name} boundaries: {total} pairs, {failures} failures");
            return failures;
        }

        private int RunFuzzBinaryOp(string name, int iterations,
            Func<BigInteger, BigInteger, BigInteger> biOp,
            Func<EvmUInt256, EvmUInt256, EvmUInt256> evmOp)
        {
            int failures = 0;
            for (int i = 0; i < iterations; i++)
            {
                var a = RandomBigInteger();
                var b = RandomBigInteger();
                var expected = biOp(a, b);
                var result = evmOp(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(a),
                    EvmUInt256BigIntegerExtensions.FromBigInteger(b));
                var actual = EvmUInt256BigIntegerExtensions.ToBigInteger(result);
                if (expected != actual)
                {
                    if (failures < 3)
                        _output.WriteLine($"{name} FAIL [{i}]: a bits={a.GetBitLength()}, b bits={b.GetBitLength()}");
                    failures++;
                }
            }
            _output.WriteLine($"{name} fuzz: {iterations} iterations, {failures} failures");
            return failures;
        }

        private void Check(string op, BigInteger expected, EvmUInt256 actual, ref int failures)
        {
            var rt = EvmUInt256BigIntegerExtensions.ToBigInteger(actual);
            if (expected != rt)
            {
                if (failures < 3)
                    _output.WriteLine($"{op} FAIL: expected={expected}, got={rt}");
                failures++;
            }
        }

        private static BigInteger RandomBigInteger()
        {
            var bytes = new byte[32];
            lock (Rng) { Rng.NextBytes(bytes); }
            bytes[31] &= 0x7F;
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
            if (value.Sign < 0) value = -value;
            if (value > MAX_UINT256) value &= MAX_UINT256;
            return value;
        }
    }
}
