using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round5
{
    public class S7R5HostedTests : IDisposable
    {
        private const int InvalidParams = -32602;
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        private const string Eip7702Fields =
            "\"sender\":\"0x1111111111111111111111111111111111111111\",\"nonce\":\"0x0\",\"factory\":\"0x7702\"," +
            "\"callData\":\"0x\",\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0xc350\"," +
            "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"," +
            "\"eip7702Auth\":{\"chainId\":\"0x539\",\"address\":\"0x1234567890123456789012345678901234567890\",\"nonce\":\"0x0\"," +
            "\"yParity\":\"0x1\",\"r\":\"0x1111111111111111111111111111111111111111111111111111111111111111\"," +
            "\"s\":\"0x2222222222222222222222222222222222222222222222222222222222222222\"}";

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Fact]
        [Trait("Rule", "S7-R5-07")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 UserOperation")]
        public async Task SendUserOperation_WhenTheFactoryIsTheEip7702Sentinel_IsNotRejectedAsInvalidParams()
        {
            var key = EthECKey.GenerateKey();
            var signed = new Authorisation7702Signer().SignAuthorisation(
                key, new Authorisation7702 { ChainId = 1337, Address = "0x1234567890123456789012345678901234567890", Nonce = 0 });
            var yParity = signed.V.Length == 0 ? "0x0" : signed.V.ToHex(true);
            var fields =
                "\"sender\":\"" + key.GetPublicAddress() + "\",\"nonce\":\"0x0\",\"factory\":\"0x7702\"," +
                "\"callData\":\"0x\",\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0xc350\"," +
                "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"," +
                "\"eip7702Auth\":{\"chainId\":\"0x539\",\"address\":\"0x1234567890123456789012345678901234567890\",\"nonce\":\"0x0\"," +
                "\"yParity\":\"" + yParity + "\",\"r\":\"" + signed.R.ToHex(true) + "\",\"s\":\"" + signed.S.ToHex(true) + "\"}";
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_sendUserOperation\",\"params\":[{" + fields + "},\"" +
                          S7R3Host.EntryPoint + "\"]}";

            var (_, body) = await _host.PostAsync(request);

            Assert.NotEqual(InvalidParams, ErrorCode(body));
        }

        [Fact]
        [Trait("Rule", "S7-R5-07")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 UserOperation")]
        public void ParseUserOperation_WhenTheFactoryIsTheEip7702Sentinel_ParsesWithoutError()
        {
            using var document = JsonDocument.Parse("{" + Eip7702Fields + "}");

            var failure = Record.Exception(() => UserOperationRpcParser.ParseUserOperation(document.RootElement));

            Assert.Null(failure);
        }
    }
}
