using System.Diagnostics;
using System.Numerics;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    [Trait("Category", "Bn254Benchmark")]
    [Trait("Category", "LargeFixture")]
    public class ManagedBn254BackendBenchmarkTests
    {
        private const int Calls = 10000;
        private const int AddCalls = 100000;
        private const int WarmupCalls = 200;
        private const int ReferenceCalls = 100;

        private readonly ITestOutputHelper _output;

        public ManagedBn254BackendBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Given_10000_ecMul_calls_on_random_points_When_the_managed_backend_runs_Then_it_reports_time_and_allocations_and_matches_the_textbook_reference()
        {
            var inputs = RandomMulInputs();
            Measure(inputs.Take(WarmupCalls).ToArray(), ManagedBn254Backend.Instance, TryMul);

            var managed = Measure(inputs, ManagedBn254Backend.Instance, TryMul);

            _output.WriteLine($"managed: {managed.Milliseconds} ms, {managed.AllocatedBytes / Calls} B/call");
            foreach (var input in inputs.Take(ReferenceCalls))
            {
                var point = (Bn254Oracle.FromBytes(input, 0), Bn254Oracle.FromBytes(input, 32));
                var expected = Bn254Oracle.EncodePoint(Bn254Oracle.Multiply(point, Bn254Oracle.FromBytes(input, 64)));
                Assert.True(ManagedBn254Backend.Instance.TryMul(input, out var actual));
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void Given_100000_ecAdd_calls_on_random_points_When_the_managed_backend_runs_Then_it_reports_time_and_allocations_and_matches_the_textbook_reference()
        {
            var inputs = RandomAddInputs();
            Measure(inputs.Take(WarmupCalls).ToArray(), ManagedBn254Backend.Instance, TryAdd);

            var managed = Measure(inputs, ManagedBn254Backend.Instance, TryAdd);

            _output.WriteLine($"managed ecAdd: {managed.Milliseconds} ms, {managed.Milliseconds * 1000.0 / AddCalls:F2} us/call, {managed.AllocatedBytes / AddCalls} B/call");
            foreach (var input in inputs.Take(ReferenceCalls))
            {
                var first = (Bn254Oracle.FromBytes(input, 0), Bn254Oracle.FromBytes(input, 32));
                var second = (Bn254Oracle.FromBytes(input, 64), Bn254Oracle.FromBytes(input, 96));
                var expected = Bn254Oracle.EncodePoint(Bn254Oracle.Add(first, second));
                Assert.True(ManagedBn254Backend.Instance.TryAdd(input, out var actual));
                Assert.Equal(expected, actual);
            }
        }

        private static byte[][] RandomAddInputs()
        {
            var random = new Random(256);
            var inputs = new byte[AddCalls][];
            for (var i = 0; i < AddCalls; i++)
                inputs[i] = RandomPoint(random).Concat(RandomPoint(random)).ToArray();
            return inputs;
        }

        private static byte[] RandomPoint(Random random)
        {
            BigInteger x;
            BigInteger? y;
            do
            {
                var bytes = new byte[32];
                random.NextBytes(bytes);
                x = Bn254Oracle.Mod(Bn254Oracle.FromBytes(bytes), Bn254Oracle.P);
                y = Bn254Oracle.SquareRoot(x * x * x + 3);
            } while (y == null);
            return Bn254Oracle.ToBytes32(x).Concat(Bn254Oracle.ToBytes32(y.Value)).ToArray();
        }

        private static bool TryMul(IBn128OutcomeBackend backend, byte[] input, out byte[] output) => backend.TryMul(input, out output);

        private static bool TryAdd(IBn128OutcomeBackend backend, byte[] input, out byte[] output) => backend.TryAdd(input, out output);

        private delegate bool Operation(IBn128OutcomeBackend backend, byte[] input, out byte[] output);

        private static byte[][] RandomMulInputs()
        {
            var random = new Random(254);
            var inputs = new byte[Calls][];
            for (var i = 0; i < Calls; i++)
            {
                var scalar = new byte[32];
                var point = RandomPoint(random);
                random.NextBytes(scalar);
                inputs[i] = point.Concat(scalar).ToArray();
            }
            return inputs;
        }

        private static (long Milliseconds, long AllocatedBytes, int Checksum) Measure(byte[][] inputs, IBn128OutcomeBackend backend, Operation operation)
        {
            var checksum = 0;
            GC.Collect();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            foreach (var input in inputs)
            {
                Assert.True(operation(backend, input, out var output));
                checksum = checksum * 31 + BitConverter.ToInt32(output, 0) + BitConverter.ToInt32(output, 60);
            }
            stopwatch.Stop();
            return (stopwatch.ElapsedMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore, checksum);
        }
    }
}
