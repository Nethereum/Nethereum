using System;
using System.Diagnostics;
using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256PerformanceTests
    {
        private readonly ITestOutputHelper _output;
        public EvmUInt256PerformanceTests(ITestOutputHelper output) => _output = output;

        const int Iterations = 100_000;

        [Fact]
        public void Perf_Addition()
        {
            var rng = new System.Random(42);
            var aBytes = new byte[32]; var bBytes = new byte[32];
            rng.NextBytes(aBytes); rng.NextBytes(bBytes);

            var evmA = EvmUInt256.FromBigEndian(aBytes);
            var evmB = EvmUInt256.FromBigEndian(bBytes);
            var biA = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
            var biB = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
            var two256 = BigInteger.Pow(2, 256);

            // Warmup
            for (int i = 0; i < 1000; i++) { var _ = evmA + evmB; }
            for (int i = 0; i < 1000; i++) { var _ = (biA + biB) % two256; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA + evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            BigInteger biResult = default;
            for (int i = 0; i < Iterations; i++) biResult = (biA + biB) % two256;
            var biTime = sw.Elapsed;

            _output.WriteLine($"Addition ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Multiplication()
        {
            var rng = new System.Random(43);
            var aBytes = new byte[32]; var bBytes = new byte[32];
            rng.NextBytes(aBytes); rng.NextBytes(bBytes);

            var evmA = EvmUInt256.FromBigEndian(aBytes);
            var evmB = EvmUInt256.FromBigEndian(bBytes);
            var biA = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
            var biB = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
            var two256 = BigInteger.Pow(2, 256);

            for (int i = 0; i < 1000; i++) { var _ = evmA * evmB; }
            for (int i = 0; i < 1000; i++) { var _ = (biA * biB) % two256; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA * evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            BigInteger biResult = default;
            for (int i = 0; i < Iterations; i++) biResult = (biA * biB) % two256;
            var biTime = sw.Elapsed;

            _output.WriteLine($"Multiplication ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Division()
        {
            var rng = new System.Random(44);
            var aBytes = new byte[32]; var bBytes = new byte[16];
            rng.NextBytes(aBytes); rng.NextBytes(bBytes);
            bBytes[0] |= 1; // ensure non-zero

            var evmA = EvmUInt256.FromBigEndian(aBytes);
            var evmB = EvmUInt256.FromBigEndian(bBytes);
            var biA = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
            var biB = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);

            for (int i = 0; i < 1000; i++) { var _ = evmA / evmB; }
            for (int i = 0; i < 1000; i++) { var _ = biA / biB; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA / evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            BigInteger biResult = default;
            for (int i = 0; i < Iterations; i++) biResult = biA / biB;
            var biTime = sw.Elapsed;

            _output.WriteLine($"Division ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Comparison()
        {
            var rng = new System.Random(45);
            var aBytes = new byte[32]; var bBytes = new byte[32];
            rng.NextBytes(aBytes); rng.NextBytes(bBytes);

            var evmA = EvmUInt256.FromBigEndian(aBytes);
            var evmB = EvmUInt256.FromBigEndian(bBytes);
            var biA = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
            var biB = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);

            for (int i = 0; i < 1000; i++) { var _ = evmA < evmB; }
            for (int i = 0; i < 1000; i++) { var _ = biA < biB; }

            var sw = Stopwatch.StartNew();
            bool evmResult = false;
            for (int i = 0; i < Iterations; i++) evmResult = evmA < evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            bool biResult = false;
            for (int i = 0; i < Iterations; i++) biResult = biA < biB;
            var biTime = sw.Elapsed;

            _output.WriteLine($"Comparison ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_ShiftLeft()
        {
            var rng = new System.Random(46);
            var aBytes = new byte[32];
            rng.NextBytes(aBytes);

            var evmA = EvmUInt256.FromBigEndian(aBytes);
            var biA = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
            var two256 = BigInteger.Pow(2, 256);
            int shift = 37;

            for (int i = 0; i < 1000; i++) { var _ = evmA << shift; }
            for (int i = 0; i < 1000; i++) { var _ = (biA << shift) % two256; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA << shift;
            var evmTime = sw.Elapsed;

            sw.Restart();
            BigInteger biResult = default;
            for (int i = 0; i < Iterations; i++) biResult = (biA << shift) % two256;
            var biTime = sw.Elapsed;

            _output.WriteLine($"ShiftLeft ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Construction_FromLong()
        {
            for (int i = 0; i < 1000; i++) { EvmUInt256 _ = 3000000L; }
            for (int i = 0; i < 1000; i++) { BigInteger _ = 3000000L; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = 3000000L;
            var evmTime = sw.Elapsed;

            sw.Restart();
            BigInteger biResult = default;
            for (int i = 0; i < Iterations; i++) biResult = 3000000L;
            var biTime = sw.Elapsed;

            _output.WriteLine($"Construction from long ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  BigInteger:  {biTime.TotalMicroseconds:F0} µs ({biTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Speedup:     {biTime.TotalMicroseconds / evmTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_StructSize()
        {
            _output.WriteLine($"EvmUInt256 size: {System.Runtime.InteropServices.Marshal.SizeOf<EvmUInt256>()} bytes (stack-allocated, no heap)");
            _output.WriteLine($"UInt128 size:    {System.Runtime.InteropServices.Marshal.SizeOf<UInt128>()} bytes (stack-allocated, no heap)");
            _output.WriteLine($"BigInteger:      heap-allocated, variable size (typically 40+ bytes for 256-bit values)");
            _output.WriteLine($"EvmInt256 size:  {System.Runtime.InteropServices.Marshal.SizeOf<EvmInt256>()} bytes (stack-allocated, no heap)");
        }

        [Fact]
        public void Perf_Addition_VsUInt128()
        {
            UInt128 u128a = (UInt128)ulong.MaxValue * 1000;
            UInt128 u128b = (UInt128)ulong.MaxValue * 777;
            var evmA = new EvmUInt256(0, 0, 0x3E7, 0xFFFFFFFFFFFFFC18);
            var evmB = new EvmUInt256(0, 0, 0x308, 0xFFFFFFFFFFFFFD07);

            for (int i = 0; i < 1000; i++) { var _ = evmA + evmB; }
            for (int i = 0; i < 1000; i++) { var _ = u128a + u128b; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA + evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            UInt128 u128Result = default;
            for (int i = 0; i < Iterations; i++) u128Result = u128a + u128b;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Addition vs UInt128 ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt128:     {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:       {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F1}x (1.0 = same, <2.0 = acceptable for 2x width)");
        }

        [Fact]
        public void Perf_Multiplication_VsUInt128()
        {
            UInt128 u128a = (UInt128)ulong.MaxValue * 1000;
            UInt128 u128b = (UInt128)ulong.MaxValue * 777;
            var evmA = new EvmUInt256(0, 0, 0x3E7, 0xFFFFFFFFFFFFFC18);
            var evmB = new EvmUInt256(0, 0, 0x308, 0xFFFFFFFFFFFFFD07);

            for (int i = 0; i < 1000; i++) { var _ = evmA * evmB; }
            for (int i = 0; i < 1000; i++) { var _ = u128a * u128b; }

            var sw = Stopwatch.StartNew();
            EvmUInt256 evmResult = default;
            for (int i = 0; i < Iterations; i++) evmResult = evmA * evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            UInt128 u128Result = default;
            for (int i = 0; i < Iterations; i++) u128Result = u128a * u128b;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Multiplication vs UInt128 ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt128:     {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:       {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Comparison_VsUInt128()
        {
            UInt128 u128a = (UInt128)ulong.MaxValue * 1000;
            UInt128 u128b = (UInt128)ulong.MaxValue * 777;
            var evmA = new EvmUInt256(0, 0, 0x3E7, 0xFFFFFFFFFFFFFC18);
            var evmB = new EvmUInt256(0, 0, 0x308, 0xFFFFFFFFFFFFFD07);

            for (int i = 0; i < 1000; i++) { var _ = evmA < evmB; }
            for (int i = 0; i < 1000; i++) { var _ = u128a < u128b; }

            var sw = Stopwatch.StartNew();
            bool evmResult = false;
            for (int i = 0; i < Iterations; i++) evmResult = evmA < evmB;
            var evmTime = sw.Elapsed;

            sw.Restart();
            bool u128Result = false;
            for (int i = 0; i < Iterations; i++) u128Result = u128a < u128b;
            var u128Time = sw.Elapsed;

            _output.WriteLine($"Comparison vs UInt128 ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmUInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  UInt128:     {u128Time.TotalMicroseconds:F0} µs ({u128Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:       {evmTime.TotalMicroseconds / u128Time.TotalMicroseconds:F1}x");
        }
    }
}
