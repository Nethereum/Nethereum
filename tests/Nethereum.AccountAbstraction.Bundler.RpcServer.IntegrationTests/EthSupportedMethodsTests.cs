using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class EthSupportedMethodsTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public EthSupportedMethodsTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SupportedEntryPoints_ReturnsConfiguredEntryPoints()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_supportedEntryPoints");

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var entryPoints = response.Result.Value;
            Assert.Equal(JsonValueKind.Array, entryPoints.ValueKind);

            var entryPointsList = new List<string>();
            foreach (var ep in entryPoints.EnumerateArray())
            {
                entryPointsList.Add(ep.GetString()!);
            }

            Assert.Single(entryPointsList);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPointsList[0].ToLower());
        }

        [Fact]
        public async Task ChainId_ReturnsCorrectChainId()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_chainId");

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var chainIdHex = response.Result.Value.GetString()!;
            var chainId = ParseHexBigInteger(chainIdHex);

            Assert.Equal(_fixture.ChainId, chainId);
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOk()
        {
            var response = await _fixture.RpcClient.GetAsync("/");

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("ok", content.ToLower());
        }

        [Fact]
        public async Task InvalidMethod_ReturnsMethodNotFound()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_nonExistentMethod");

            Assert.NotNull(response.Error);
            Assert.Equal(-32601, response.Error.Code);
        }

        [Fact]
        public async Task MissingParams_ReturnsInvalidParams()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_sendUserOperation");

            Assert.NotNull(response.Error);
        }

        private static BigInteger ParseHexBigInteger(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            if (string.IsNullOrEmpty(hex))
                return BigInteger.Zero;
            return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
