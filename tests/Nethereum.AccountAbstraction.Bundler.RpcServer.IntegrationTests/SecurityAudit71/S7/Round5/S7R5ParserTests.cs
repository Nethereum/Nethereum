using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.CoreChain.Rpc;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round5
{
    public class S7R5ParserTests
    {
        private const string Rest =
            "\"sender\":\"0x1111111111111111111111111111111111111111\",\"nonce\":\"0x0\",\"callData\":\"0x\"," +
            "\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0xc350\"," +
            "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"";

        private const string Auth =
            ",\"eip7702Auth\":{\"chainId\":\"0x539\",\"address\":\"0x1234567890123456789012345678901234567890\",\"nonce\":\"0x0\"," +
            "\"yParity\":\"0x1\",\"r\":\"0x1\",\"s\":\"0x2\"}";

        private static void ParseSend(string fields)
        {
            using var document = JsonDocument.Parse("{" + fields + "}");
            UserOperationRpcParser.ParseUserOperation(document.RootElement);
        }

        [Theory]
        [InlineData("\"factory\":\"0x7702\"", "")]
        [InlineData("\"factory\":\"0x77\"", Auth)]
        [InlineData("\"factory\":\"0x7702ab\"", Auth)]
        [InlineData("\"factory\":\"0x\"", Auth)]
        [InlineData("\"factory\":\"0X7702\"", Auth)]
        [Trait("Rule", "S7-R5-07")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 UserOperation")]
        public void ParseUserOperation_WhenTheFactoryIsNeitherAnAddressNorTheSentinelWithAuth_IsRejectedAsInvalidParams(string factory, string auth)
        {
            var failure = Assert.Throws<RpcException>(() => ParseSend(Rest + "," + factory + auth));

            Assert.Equal(-32602, failure.Code);
        }

        [Fact]
        [Trait("Rule", "S7-R5-07")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 UserOperation")]
        public void ParseUserOperation_WhenTheFactoryIsAnAddress_IsAccepted()
        {
            var failure = Record.Exception(() => ParseSend(Rest + ",\"factory\":\"0x1111111111111111111111111111111111111111\""));

            Assert.Null(failure);
        }
    }
}
