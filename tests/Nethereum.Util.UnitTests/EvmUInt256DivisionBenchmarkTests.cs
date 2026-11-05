using System;
using System.Diagnostics;
using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    [Trait("Category", "Benchmark")]
    [Trait("Category", "LargeFixture")]
    public class EvmUInt256DivisionBenchmarkTests
    {
        private const int OperandSets = 256;
        private const int Operations = 200_000;
        private const int Repetitions = 31;

        private readonly ITestOutputHelper _output;

        public EvmUInt256DivisionBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static EvmUInt256 RandomWithBits(System.Random random, int bits)
        {
            var bytes = new byte[32];
            random.NextBytes(bytes);
            var mask = (BigInteger.One << bits) - 1;
            var value = (new BigInteger(bytes, isUnsigned: true, isBigEndian: true) & mask) | (BigInteger.One << (bits - 1));
            return EvmUInt256.FromBigEndian(value.ToByteArray(isUnsigned: true, isBigEndian: true));
        }

        private static EvmUInt256[] Operands(System.Random random, int bits)
        {
            var values = new EvmUInt256[OperandSets];
            for (var i = 0; i < values.Length; i++) values[i] = RandomWithBits(random, bits);
            return values;
        }

        private double MinNanosecondsPerOperation(Func<int, EvmUInt256> operation)
        {
            var best = double.MaxValue;
            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var sink = EvmUInt256.Zero;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < Operations; i++) sink = operation(i % OperandSets);
                var elapsed = watch.Elapsed.TotalMilliseconds * 1_000_000 / Operations;
                GC.KeepAlive(sink);
                if (repetition > 0 && elapsed < best) best = elapsed;
            }
            return best;
        }

        [Theory]
        [InlineData(255)]
        [InlineData(191)]
        [InlineData(127)]
        [InlineData(63)]
        public void Given_RandomOperands_When_MultiplyingModulo_Then_TheNanosecondsPerOperationAreReported(int modulusBits)
        {
            var random = new System.Random(modulusBits);
            var left = Operands(random, 256);
            var right = Operands(random, 256);
            var modulus = Operands(random, modulusBits);

            var nanoseconds = MinNanosecondsPerOperation(i => EvmUInt256.MulMod(left[i], right[i], modulus[i]));

            _output.WriteLine($"MULMOD modulus {modulusBits} bits: {nanoseconds:F1} ns/op");
        }

        [Theory]
        [InlineData(255)]
        [InlineData(127)]
        public void Given_RandomOperands_When_AddingModulo_Then_TheNanosecondsPerOperationAreReported(int modulusBits)
        {
            var random = new System.Random(modulusBits + 1);
            var left = Operands(random, 256);
            var right = Operands(random, 256);
            var modulus = Operands(random, modulusBits);

            var nanoseconds = MinNanosecondsPerOperation(i => EvmUInt256.AddMod(left[i], right[i], modulus[i]));

            _output.WriteLine($"ADDMOD modulus {modulusBits} bits: {nanoseconds:F1} ns/op");
        }

        [Theory]
        [InlineData(255)]
        [InlineData(127)]
        [InlineData(63)]
        public void Given_RandomOperands_When_Dividing_Then_TheNanosecondsPerOperationAreReported(int divisorBits)
        {
            var random = new System.Random(divisorBits + 2);
            var dividend = Operands(random, 256);
            var divisor = Operands(random, divisorBits);

            var nanoseconds = MinNanosecondsPerOperation(i => dividend[i] / divisor[i]);

            _output.WriteLine($"DIV divisor {divisorBits} bits: {nanoseconds:F1} ns/op");
        }
    }
}
