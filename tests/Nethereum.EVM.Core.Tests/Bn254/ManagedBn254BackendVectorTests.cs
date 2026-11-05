using System.Text.Json;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class ManagedBn254BackendVectorTests
    {
        private const string GethVectors = "Bn254/TestData/go-ethereum-36a7dc72e/core-vm-testdata-precompiles";

        public static TheoryData<string, string, string> AddVectors() => Load("bn256Add.json");

        public static TheoryData<string, string, string> MulVectors() => Load("bn256ScalarMul.json");

        private static TheoryData<string, string, string> Load(string file)
        {
            var data = new TheoryData<string, string, string>();
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, GethVectors, file));
            foreach (var vector in JsonDocument.Parse(json).RootElement.EnumerateArray())
                data.Add(vector.GetProperty("Name").GetString(), vector.GetProperty("Input").GetString(), vector.GetProperty("Expected").GetString());
            return data;
        }

        [Theory]
        [MemberData(nameof(AddVectors))]
        public void Given_geth_bn256Add_vectors_When_executed_by_the_managed_backend_Then_every_output_matches(string name, string input, string expected)
        {
            var backend = ManagedBn254Backend.Instance;

            Assert.True(backend.TryAdd(Convert.FromHexString(input), out var output), name);
            Assert.Equal(expected, Convert.ToHexString(output).ToLowerInvariant());
            Assert.Equal(expected, Convert.ToHexString(backend.Add(Convert.FromHexString(input))).ToLowerInvariant());
        }

        [Theory]
        [MemberData(nameof(MulVectors))]
        public void Given_geth_bn256ScalarMul_vectors_When_executed_by_the_managed_backend_Then_every_output_matches(string name, string input, string expected)
        {
            var backend = ManagedBn254Backend.Instance;

            Assert.True(backend.TryMul(Convert.FromHexString(input), out var output), name);
            Assert.Equal(expected, Convert.ToHexString(output).ToLowerInvariant());
            Assert.Equal(expected, Convert.ToHexString(backend.Mul(Convert.FromHexString(input))).ToLowerInvariant());
        }

        [Fact]
        public void Given_the_geth_vector_files_When_loaded_Then_they_contain_the_expected_number_of_vectors()
        {
            Assert.Equal(16, AddVectors().Count);
            Assert.Equal(19, MulVectors().Count);
        }
    }
}
