using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct UInt256Via128
    {
        public readonly UInt128 Lower;
        public readonly UInt128 Upper;

        public static readonly UInt256Via128 Zero = default;
        public static readonly UInt256Via128 One = new(0, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt256Via128(UInt128 upper, UInt128 lower) { Upper = upper; Lower = lower; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt256Via128(ulong value) : this(0, value) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt256Via128 operator +(UInt256Via128 a, UInt256Via128 b)
        {
            UInt128 lower = a.Lower + b.Lower;
            UInt128 carry = lower < a.Lower ? (UInt128)1 : 0;
            UInt128 upper = a.Upper + b.Upper + carry;
            return new UInt256Via128(upper, lower);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt256Via128 operator -(UInt256Via128 a, UInt256Via128 b)
        {
            UInt128 lower = a.Lower - b.Lower;
            UInt128 borrow = a.Lower < b.Lower ? (UInt128)1 : 0;
            UInt128 upper = a.Upper - b.Upper - borrow;
            return new UInt256Via128(upper, lower);
        }

        public static UInt256Via128 operator *(UInt256Via128 a, UInt256Via128 b)
        {
            // Split each UInt128 into (hi64, lo64) and use Math.BigMul
            ulong aLo = (ulong)a.Lower;
            ulong aHi = (ulong)(a.Lower >> 64);
            ulong bLo = (ulong)b.Lower;
            ulong bHi = (ulong)(b.Lower >> 64);

            // p0 = aLo * bLo (full 128 bits)
            ulong p0Hi = Math.BigMul(aLo, bLo, out ulong p0Lo);

            // p1 = aHi * bLo (only need lower 128 bits for result)
            ulong p1Hi = Math.BigMul(aHi, bLo, out ulong p1Lo);

            // p2 = aLo * bHi
            ulong p2Hi = Math.BigMul(aLo, bHi, out ulong p2Lo);

            // Lower 128 bits: p0Lo + (p0Hi + p1Lo + p2Lo) << 64
            // But we only keep lower 64 of the middle sum shifted
            UInt128 lower = (UInt128)p0Lo | ((UInt128)(p0Hi + p1Lo + p2Lo) << 64);

            // We don't need upper 128 bits to be exact (truncation mod 2^256 like EVM)
            // But for correctness, compute upper carry
            // Upper = carry from middle + p1Hi + p2Hi + aHi*bHi + cross terms with a.Upper, b.Upper
            UInt128 mid = (UInt128)p0Hi + p1Lo + p2Lo;
            UInt128 upper = (UInt128)p1Hi + p2Hi + (mid >> 64)
                + (UInt128)aHi * bHi
                + a.Upper * b.Lower
                + a.Lower * b.Upper;

            return new UInt256Via128(upper, lower);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(UInt256Via128 a, UInt256Via128 b)
            => a.Upper < b.Upper || (a.Upper == b.Upper && a.Lower < b.Lower);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(UInt256Via128 a, UInt256Via128 b) => b < a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UInt256Via128 a, UInt256Via128 b)
            => a.Lower == b.Lower && a.Upper == b.Upper;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UInt256Via128 a, UInt256Via128 b) => !(a == b);

        public override bool Equals(object obj) => obj is UInt256Via128 other && this == other;
        public override int GetHashCode() => Lower.GetHashCode() ^ Upper.GetHashCode();
    }

    public class UInt128BasedUInt256Benchmark
    {
        private readonly ITestOutputHelper _output;
        public UInt128BasedUInt256Benchmark(ITestOutputHelper output) => _output = output;

        const int Iterations = 1_000_000;

        [Fact]
        public void Perf_Addition_ThreeWay()
        {
            var rng = new System.Random(42);
            var bytes = new byte[32];
            rng.NextBytes(bytes);
            var bytes2 = new byte[32];
            rng.NextBytes(bytes2);

            var evmA = EvmUInt256.FromBigEndian(bytes);
            var evmB = EvmUInt256.FromBigEndian(bytes2);

            var u128A = new UInt256Via128(
                new UInt128((ulong)((evmA.U3)), (ulong)(evmA.U2)),
                new UInt128((ulong)(evmA.U1), (ulong)(evmA.U0)));
            var u128B = new UInt256Via128(
                new UInt128((ulong)(evmB.U3), (ulong)(evmB.U2)),
                new UInt128((ulong)(evmB.U1), (ulong)(evmB.U0)));

            // Warmup
            for (int i = 0; i < 10000; i++) { var _ = evmA + evmB; }
            for (int i = 0; i < 10000; i++) { var _ = u128A + u128B; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = evmA + evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            UInt256Via128 r2 = default;
            for (int i = 0; i < Iterations; i++) r2 = u128A + u128B;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Addition ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256 (4×ulong):   {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt256Via128 (2×U128): {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio (4×ulong / 2×U128): {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F2}x");
        }

        [Fact]
        public void Perf_Multiplication_ThreeWay()
        {
            var rng = new System.Random(43);
            var bytes = new byte[32];
            rng.NextBytes(bytes);
            var bytes2 = new byte[32];
            rng.NextBytes(bytes2);

            var evmA = EvmUInt256.FromBigEndian(bytes);
            var evmB = EvmUInt256.FromBigEndian(bytes2);

            var u128A = new UInt256Via128(
                new UInt128(evmA.U3, evmA.U2),
                new UInt128(evmA.U1, evmA.U0));
            var u128B = new UInt256Via128(
                new UInt128(evmB.U3, evmB.U2),
                new UInt128(evmB.U1, evmB.U0));

            for (int i = 0; i < 10000; i++) { var _ = evmA * evmB; }
            for (int i = 0; i < 10000; i++) { var _ = u128A * u128B; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = evmA * evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            UInt256Via128 r2 = default;
            for (int i = 0; i < Iterations; i++) r2 = u128A * u128B;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Multiplication ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256 (4×ulong):   {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt256Via128 (2×U128): {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio (4×ulong / 2×U128): {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F2}x");
        }

        [Fact]
        public void Perf_Comparison_ThreeWay()
        {
            var rng = new System.Random(44);
            var bytes = new byte[32];
            rng.NextBytes(bytes);
            var bytes2 = new byte[32];
            rng.NextBytes(bytes2);

            var evmA = EvmUInt256.FromBigEndian(bytes);
            var evmB = EvmUInt256.FromBigEndian(bytes2);

            var u128A = new UInt256Via128(
                new UInt128(evmA.U3, evmA.U2),
                new UInt128(evmA.U1, evmA.U0));
            var u128B = new UInt256Via128(
                new UInt128(evmB.U3, evmB.U2),
                new UInt128(evmB.U1, evmB.U0));

            for (int i = 0; i < 10000; i++) { var _ = evmA < evmB; }
            for (int i = 0; i < 10000; i++) { var _ = u128A < u128B; }

            var sw = Stopwatch.StartNew();
            bool r1 = false;
            for (int i = 0; i < Iterations; i++) r1 = evmA < evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            bool r2 = false;
            for (int i = 0; i < Iterations; i++) r2 = u128A < u128B;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Comparison ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256 (4×ulong):   {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt256Via128 (2×U128): {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio (4×ulong / 2×U128): {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F2}x");
        }

        [Fact]
        public void Correctness_Mul_Matches()
        {
            var rng = new System.Random(99);
            var two256 = BigInteger.Pow(2, 256);
            for (int i = 0; i < 1000; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);

                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var expected = (aBi * bBi) % two256;

                var evmA = EvmUInt256.FromBigEndian(aBytes);
                var evmB = EvmUInt256.FromBigEndian(bBytes);

                var u128A = new UInt256Via128(
                    new UInt128(evmA.U3, evmA.U2),
                    new UInt128(evmA.U1, evmA.U0));
                var u128B = new UInt256Via128(
                    new UInt128(evmB.U3, evmB.U2),
                    new UInt128(evmB.U1, evmB.U0));

                var r = u128A * u128B;
                ulong r0 = (ulong)r.Lower;
                ulong r1 = (ulong)(r.Lower >> 64);
                ulong r2 = (ulong)r.Upper;
                ulong r3 = (ulong)(r.Upper >> 64);
                var resultEvm = new EvmUInt256(r3, r2, r1, r0);
                Assert.Equal(expected, resultEvm.ToBigInteger());
            }
        }
    }
}
