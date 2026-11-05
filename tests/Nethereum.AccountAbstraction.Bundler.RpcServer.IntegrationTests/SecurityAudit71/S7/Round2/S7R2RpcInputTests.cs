using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round2
{
    public class S7R2RpcInputTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private const string Address = "0x1111111111111111111111111111111111111111";
        private const int InvalidParams = -32602;

        private readonly BundlerService _bundler;
        private readonly RpcDispatcher _dispatcher;

        public S7R2RpcInputTests()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = Address,
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

        private static Dictionary<string, string> ValidFields() => new()
        {
            ["sender"] = Address,
            ["nonce"] = "0x0",
            ["callData"] = "0x",
            ["callGasLimit"] = "0x186a0",
            ["verificationGasLimit"] = "0x249f0",
            ["preVerificationGas"] = "0xc350",
            ["maxFeePerGas"] = "0x77359400",
            ["maxPriorityFeePerGas"] = "0x3b9aca00",
            ["signature"] = "0x"
        };

        private async Task<RpcResponseMessage> CallAsync(string method, string paramsJson)
        {
            using var doc = JsonDocument.Parse(paramsJson);
            return await _dispatcher.DispatchAsync(new RpcRequestMessage
            {
                Id = 1,
                Method = method,
                RawParameters = doc.RootElement.Clone()
            });
        }

        private static string OpParams(Dictionary<string, string> fields, string entryPointJson = "\"" + EntryPoint + "\"") =>
            "[" + JsonSerializer.Serialize(fields) + "," + entryPointJson + "]";

        [Theory]
        [InlineData("eth_sendUserOperation", "callData", "0XABCD")]
        [InlineData("eth_sendUserOperation", "signature", "0XABCD")]
        [InlineData("eth_sendUserOperation", "factory", "0X1111111111111111111111111111111111111111")]
        [InlineData("eth_sendUserOperation", "paymaster", "0X1111111111111111111111111111111111111111")]
        [InlineData("eth_sendUserOperation", "sender", "0X1111111111111111111111111111111111111111")]
        [InlineData("eth_estimateUserOperationGas", "callData", "0XABCD")]
        [InlineData("eth_estimateUserOperationGas", "factory", "0X1111111111111111111111111111111111111111")]
        [InlineData("eth_estimateUserOperationGas", "paymaster", "0X1111111111111111111111111111111111111111")]
        [InlineData("eth_estimateUserOperationGas", "sender", "0X1111111111111111111111111111111111111111")]
        [Trait("Rule", "S7-R2-5")]
        [Trait("Erc", "ERC-7769 invalid fields")]
        public async Task UserOperationMethod_WithUpperCaseXHexPrefix_IsRejectedAsInvalidParamsNotAnInternalError(
            string method, string field, string value)
        {
            var fields = ValidFields();
            fields[field] = value;
            if (field == "factory") fields["factoryData"] = "0x";

            var response = await CallAsync(method, OpParams(fields));

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }

        [Theory]
        [InlineData("eth_sendUserOperation", "5")]
        [InlineData("eth_sendUserOperation", "true")]
        [InlineData("eth_sendUserOperation", "{}")]
        [InlineData("eth_sendUserOperation", "[]")]
        [InlineData("eth_estimateUserOperationGas", "5")]
        [InlineData("eth_estimateUserOperationGas", "true")]
        [InlineData("eth_estimateUserOperationGas", "{}")]
        [InlineData("eth_estimateUserOperationGas", "[]")]
        [Trait("Rule", "S7-R2-6")]
        [Trait("Erc", "ERC-7769 invalid fields")]
        public async Task UserOperationMethod_WithANonStringEntryPoint_IsRejectedAsInvalidParams(string method, string entryPointJson)
        {
            var response = await CallAsync(method, OpParams(ValidFields(), entryPointJson));

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }

        [Theory]
        [InlineData("sender")]
        [InlineData("nonce")]
        [InlineData("callData")]
        [InlineData("callGasLimit")]
        [InlineData("verificationGasLimit")]
        [InlineData("preVerificationGas")]
        [InlineData("maxFeePerGas")]
        [InlineData("maxPriorityFeePerGas")]
        [InlineData("signature")]
        [Trait("Rule", "S7-R2-7")]
        [Trait("Erc", "ERC-7769 invalid fields")]
        public async Task SendUserOperation_WhenARequiredFieldIsMissing_IsRejectedAsInvalidParamsBeforeAnyNodeCall(string missing)
        {
            var fields = ValidFields();
            fields.Remove(missing);

            var response = await CallAsync("eth_sendUserOperation", OpParams(fields));

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }

        [Fact]
        [Trait("Rule", "S7-R2-8")]
        [Trait("Erc", "ERC-7562 LIM-010")]
        public async Task EstimateUserOperationGas_WithACallDataLargerThanTheAdmissionSizeLimit_IsRejectedBeforeAnyNodeCall()
        {
            var fields = ValidFields();
            fields["callData"] = "0x" + new string('a', 400_000);

            var response = await CallAsync("eth_estimateUserOperationGas", OpParams(fields));

            Assert.NotNull(response.Error);
            Assert.Equal(InvalidParams, response.Error.Code);
        }
    }
}
