using System;
using System.Diagnostics;
using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class EvmInt256PerformanceTests
    {
        private readonly ITestOutputHelper _output;
        public EvmInt256PerformanceTests(ITestOutputHelper output) => _output = output;

        const int Iterations = 1_000_000;

        [Fact]
        public void Perf_SignedAdd()
        {
            EvmInt256 a = 3000000L;
            EvmInt256 b = -21000L;
            long la = 3000000L, lb = -21000L;

            for (int i = 0; i < 10000; i++) { var _ = a + b; }
            for (int i = 0; i < 10000; i++) { var _ = la + lb; }

            var sw = Stopwatch.StartNew();
            EvmInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = a + b;
            var evmTime = sw.Elapsed;

            sw.Restart();
            long r2 = 0;
            for (int i = 0; i < Iterations; i++) r2 = la + lb;
            var longTime = sw.Elapsed;

            _output.WriteLine($"Signed Addition ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_SignedSub()
        {
            EvmInt256 a = 3000000L;
            EvmInt256 b = 5000000L;
            long la = 3000000L, lb = 5000000L;

            for (int i = 0; i < 10000; i++) { var _ = a - b; }
            for (int i = 0; i < 10000; i++) { var _ = la - lb; }

            var sw = Stopwatch.StartNew();
            EvmInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = a - b;
            var evmTime = sw.Elapsed;

            sw.Restart();
            long r2 = 0;
            for (int i = 0; i < Iterations; i++) r2 = la - lb;
            var longTime = sw.Elapsed;

            _output.WriteLine($"Signed Subtraction ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_SignedCompare()
        {
            EvmInt256 a = -21000L;
            EvmInt256 b = 3000000L;
            long la = -21000L, lb = 3000000L;

            for (int i = 0; i < 10000; i++) { var _ = a < b; }
            for (int i = 0; i < 10000; i++) { var _ = la < lb; }

            var sw = Stopwatch.StartNew();
            bool r1 = false;
            for (int i = 0; i < Iterations; i++) r1 = a < b;
            var evmTime = sw.Elapsed;

            sw.Restart();
            bool r2 = false;
            for (int i = 0; i < Iterations; i++) r2 = la < lb;
            var longTime = sw.Elapsed;

            _output.WriteLine($"Signed Comparison ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_SignedCompare_VsLong()
        {
            // Compare EvmInt256 < long (the gas check pattern: gasRemaining < 0)
            EvmInt256 a = -500L;

            for (int i = 0; i < 10000; i++) { var _ = a < 0L; }

            var sw = Stopwatch.StartNew();
            bool r1 = false;
            for (int i = 0; i < Iterations; i++) r1 = a < 0L;
            var evmTime = sw.Elapsed;

            // vs just checking IsNegative
            sw.Restart();
            bool r2 = false;
            for (int i = 0; i < Iterations; i++) r2 = a.IsNegative;
            var propTime = sw.Elapsed;

            _output.WriteLine($"Gas check: value < 0 ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256 < 0L:    {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  .IsNegative:       {propTime.TotalMicroseconds:F0} µs ({propTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
        }

        [Fact]
        public void Perf_SignedDiv()
        {
            EvmInt256 a = -3000000L;
            EvmInt256 b = 7L;
            long la = -3000000L, lb = 7L;

            for (int i = 0; i < 10000; i++) { var _ = a / b; }
            for (int i = 0; i < 10000; i++) { var _ = la / lb; }

            var sw = Stopwatch.StartNew();
            EvmInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = a / b;
            var evmTime = sw.Elapsed;

            sw.Restart();
            long r2 = 0;
            for (int i = 0; i < Iterations; i++) r2 = la / lb;
            var longTime = sw.Elapsed;

            _output.WriteLine($"Signed Division ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_SignedMul()
        {
            EvmInt256 a = -21000L;
            EvmInt256 b = 20L;
            long la = -21000L, lb = 20L;

            for (int i = 0; i < 10000; i++) { var _ = a * b; }
            for (int i = 0; i < 10000; i++) { var _ = la * lb; }

            var sw = Stopwatch.StartNew();
            EvmInt256 r1 = default;
            for (int i = 0; i < Iterations; i++) r1 = a * b;
            var evmTime = sw.Elapsed;

            sw.Restart();
            long r2 = 0;
            for (int i = 0; i < Iterations; i++) r2 = la * lb;
            var longTime = sw.Elapsed;

            _output.WriteLine($"Signed Multiplication ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_SignedCompare_VsManualOnUInt256()
        {
            // Current EVM pattern: manual sign check on EvmUInt256
            var uA = new EvmUInt256(-21000L); // negative as two's complement
            var uB = new EvmUInt256(3000000L);

            // EvmInt256 signed compare
            var sA = (EvmInt256)uA;
            var sB = (EvmInt256)uB;

            for (int i = 0; i < 10000; i++) { var _ = sA < sB; }
            for (int i = 0; i < 10000; i++)
            {
                bool aN = uA.IsHighBitSet;
                bool bN = uB.IsHighBitSet;
                var _ = aN != bN ? aN : uA < uB;
            }

            var sw = Stopwatch.StartNew();
            bool r1 = false;
            for (int i = 0; i < Iterations; i++) r1 = sA < sB;
            var int256Time = sw.Elapsed;

            sw.Restart();
            bool r2 = false;
            for (int i = 0; i < Iterations; i++)
            {
                bool aN = uA.IsHighBitSet;
                bool bN = uB.IsHighBitSet;
                r2 = aN != bN ? aN : uA < uB;
            }
            var manualTime = sw.Elapsed;

            _output.WriteLine($"Signed Compare: EvmInt256 vs Manual on EvmUInt256 ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256 operator<:   {int256Time.TotalMicroseconds:F0} µs ({int256Time.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Manual IsHighBitSet:   {manualTime.TotalMicroseconds:F0} µs ({manualTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:                 {int256Time.TotalMicroseconds / manualTime.TotalMicroseconds:F2}x");
        }

        [Fact]
        public void Perf_GasSimulation()
        {
            // Simulate a typical gas accounting loop:
            // Start with gasLimit, subtract costs, check if negative
            long gasLimitL = 3000000;
            long[] costsL = { 21000, 3, 3, 5, 200, 20000, 3, 3, 5, 700 };

            EvmInt256 gasLimitE = 3000000L;
            EvmInt256[] costsE = new EvmInt256[costsL.Length];
            for (int i = 0; i < costsL.Length; i++) costsE[i] = costsL[i];

            // Warmup
            for (int w = 0; w < 1000; w++)
            {
                long rem = gasLimitL;
                for (int i = 0; i < costsL.Length; i++) { rem -= costsL[i]; if (rem < 0) break; }
            }
            for (int w = 0; w < 1000; w++)
            {
                EvmInt256 rem = gasLimitE;
                for (int i = 0; i < costsE.Length; i++) { rem = rem - costsE[i]; if (rem.IsNegative) break; }
            }

            int iters = 100_000;

            var sw = Stopwatch.StartNew();
            for (int w = 0; w < iters; w++)
            {
                long rem = gasLimitL;
                for (int i = 0; i < costsL.Length; i++) { rem -= costsL[i]; if (rem < 0) break; }
            }
            var longTime = sw.Elapsed;

            sw.Restart();
            for (int w = 0; w < iters; w++)
            {
                EvmInt256 rem = gasLimitE;
                for (int i = 0; i < costsE.Length; i++) { rem = rem - costsE[i]; if (rem.IsNegative) break; }
            }
            var evmTime = sw.Elapsed;

            _output.WriteLine($"Gas accounting loop (subtract 10 costs, check negative) × {iters:N0}:");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / iters * 1000:F1} ns/iter)");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / iters * 1000:F1} ns/iter)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }

        [Fact]
        public void Perf_Abs()
        {
            EvmInt256 a = -3000000L;

            for (int i = 0; i < 10000; i++) { var _ = a.Abs(); }

            var sw = Stopwatch.StartNew();
            EvmUInt256 r = default;
            for (int i = 0; i < Iterations; i++) r = a.Abs();
            var evmTime = sw.Elapsed;

            // vs Math.Abs(long)
            long la = -3000000L;
            sw.Restart();
            long r2 = 0;
            for (int i = 0; i < Iterations; i++) r2 = Math.Abs(la);
            var longTime = sw.Elapsed;

            _output.WriteLine($"Abs ({Iterations:N0} iterations):");
            _output.WriteLine($"  EvmInt256:  {evmTime.TotalMicroseconds:F0} µs ({evmTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  long:       {longTime.TotalMicroseconds:F0} µs ({longTime.TotalMicroseconds / Iterations * 1000:F1} ns/op)");
            _output.WriteLine($"  Ratio:      {evmTime.TotalMicroseconds / longTime.TotalMicroseconds:F1}x");
        }
    }
}
