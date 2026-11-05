using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7
{
    public class S7RpcInputTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private const int InvalidParams = -32602;

        private readonly BundlerService _bundler;
        private readonly RpcDispatcher _dispatcher;

        public S7RpcInputTests()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = "0x1111111111111111111111111111111111111111",
                AutoBundleIntervalMs = 0,
                ReputationDecayIntervalMs = 0,
                ChainId = 1337
            };
            _bundler = new BundlerService(new Nethereum.Web3.Web3("http://127.0.0.1:1"), config);

            var registry = new RpcHandlerRegistry();
            registry.AddBundlerHandlers(_bundler);
            var context = new RpcContext(
                (Func<Nethereum.CoreChain.IChainNode>)(() => throw new InvalidOperationException("no node")),
                new BigInteger(1337),
                new ServiceCollection().BuildServiceProvider());
            _dispatcher = new RpcDispatcher(registry, context, null);
        }

        public void Dispose()
        {
            _bundler.Dispose();
        }

        private static string UserOpJson(string field, string value)
        {
            var fields = new Dictionary<string, string>
            {
                ["sender"] = "0x1111111111111111111111111111111111111111",
                ["nonce"] = "0x0",
                ["callData"] = "0x",
                ["callGasLimit"] = "0x186a0",
                ["verificationGasLimit"] = "0x249f0",
                ["preVerificationGas"] = "0xc350",
                ["maxFeePerGas"] = "0x77359400",
                ["maxPriorityFeePerGas"] = "0x3b9aca00",
                ["signature"] = "0x"
            };
            fields[field] = value;
            return JsonSerializer.Serialize(fields);
        }

        private async Task<RpcResponseMessage> SendAsync(string userOpJson)
        {
            using var doc = JsonDocument.Parse(userOpJson);
            var request = new RpcRequestMessage(1, "eth_sendUserOperation", doc.RootElement.Clone(), EntryPoint);
            return await _dispatcher.DispatchAsync(request);
        }

        [Theory]
        [InlineData("callData", "0xzz")]
        [InlineData("callData", "0xabc")]
        [InlineData("signature", "not-hex")]
        [InlineData("sender", "0x12")]
        [InlineData("nonce", "0x1000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("maxFeePerGas", "0x100000000000000000000000000000000")]
        [InlineData("callGasLimit", "0xzz")]
        [Trait("Rule", "S7-10")]
        [Trait("Erc", "ERC-4337 input validation")]
        public async Task SendUserOperation_WithHostileFieldEncoding_IsRejectedAsInvalidParamsBeforeAnyNodeCall(string field, string value)
        {
            var response = await SendAsync(UserOpJson(field, value));

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }

        [Theory]
        [InlineData("0xzz")]
        [InlineData("0x")]
        [InlineData("0x1234")]
        [InlineData("not-a-hash")]
        [Trait("Rule", "S7-10")]
        [Trait("Erc", "ERC-4337 input validation")]
        public async Task GetUserOperationReceipt_WithMalformedHash_IsRejectedAsInvalidParamsBeforeAnyNodeCall(string hash)
        {
            var request = new RpcRequestMessage(1, "eth_getUserOperationReceipt", hash);

            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }

        [Fact]
        [Trait("Rule", "S7-11")]
        [Trait("Erc", "ERC-4337 input limits")]
        public async Task DispatchBatch_WithMoreRequestsThanTheBatchLimit_IsRejectedWholesale()
        {
            var requests = Enumerable.Range(0, 1001)
                .Select(i => new RpcRequestMessage(i, "eth_supportedEntryPoints"))
                .ToArray();

            var responses = await _dispatcher.DispatchBatchAsync(requests);

            Assert.Single(responses);
            Assert.NotNull(responses[0].Error);
        }

        [Fact]
        [Trait("Rule", "S7-12")]
        [Trait("Erc", "ERC-7562 validation scope rules")]
        public void DefaultServerConfig_EnforcesErc7562ValidationRules()
        {
            var bundlerConfig = new BundlerRpcServerConfig().ToBundlerConfig();

            Assert.True(bundlerConfig.EnableERC7562Validation);
        }
    }
}
