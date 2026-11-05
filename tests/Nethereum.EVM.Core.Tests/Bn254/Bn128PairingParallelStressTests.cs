using System.Collections.Concurrent;
using System.Text.Json;
using Nethereum.Signer.Crypto;
using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class Bn128PairingParallelStressTests
    {
        private const string GethVectors = "Bn254/TestData/go-ethereum-36a7dc72e/core-vm-testdata-precompiles/bn256Pairing.json";
        private const int Iterations = 200000;

        [Fact]
        [Trait("Category", "Bn254Differential")]
        [Trait("Category", "LargeFixture")]
        public void Given_the_EIP197_pairing_vectors_When_run_in_parallel_for_200000_iterations_Then_every_answer_is_the_known_answer()
        {
            var vectors = Load();
            var failures = new ConcurrentBag<string>();

            Parallel.For(0, Iterations, index =>
            {
                var vector = vectors[index % vectors.Length];
                if (!BN128Curve.TryPairing(vector.Input, out var output) || !output.AsSpan().SequenceEqual(vector.Expected))
                    failures.Add($"{vector.Name} iteration={index}");
            });

            Assert.True(failures.IsEmpty, $"{failures.Count} wrong answers, first: {string.Join(", ", failures.Take(5))}");
        }

        [Fact]
        public void Given_the_EIP197_pairing_vectors_When_run_once_Then_every_answer_is_the_known_answer()
        {
            var vectors = Load();

            Assert.Equal(14, vectors.Length);
            foreach (var vector in vectors)
            {
                Assert.True(BN128Curve.TryPairing(vector.Input, out var output), vector.Name);
                Assert.Equal(vector.Expected, output);
            }
        }

        private static (string Name, byte[] Input, byte[] Expected)[] Load()
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, GethVectors));
            return JsonDocument.Parse(json).RootElement.EnumerateArray()
                .Select(v => (v.GetProperty("Name").GetString(), Convert.FromHexString(v.GetProperty("Input").GetString()), Convert.FromHexString(v.GetProperty("Expected").GetString())))
                .ToArray();
        }
    }
}
