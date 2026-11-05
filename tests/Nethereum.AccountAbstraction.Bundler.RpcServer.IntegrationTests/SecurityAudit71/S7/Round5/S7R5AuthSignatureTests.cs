using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round5
{
    public class S7R5AuthSignatureTests : IDisposable
    {
        private const string SignatureInvalid = "Authorization signature is invalid";
        private const string SignerNotSender = "Authorization signer is not sender";
        private const string HighS = "0xfffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364140";
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        [Theory]
        [InlineData("0x0", "0x0", "0x1", SignatureInvalid)]
        [InlineData("0x1", "0x1", "0x0", SignatureInvalid)]
        [InlineData("0x1", "0x1", HighS, SignatureInvalid)]
        [InlineData("0x1", "0x1111111111111111111111111111111111111111111111111111111111111111", "0x2222222222222222222222222222222222222222222222222222222222222222", "")]
        [Trait("Rule", "S7-R5-11")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 UserOperation")]
        public async Task SendUserOperation_WhenTheAuthorisationSignatureIsUnrecoverable_IsRejectedAsInvalidParamsWithAFixedMessage(
            string yParity, string r, string s, string expectedMessage)
        {
            var fields =
                "\"sender\":\"0x1111111111111111111111111111111111111111\",\"nonce\":\"0x0\",\"factory\":\"0x7702\"," +
                "\"callData\":\"0x\",\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0xc350\"," +
                "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"," +
                "\"eip7702Auth\":{\"chainId\":\"0x539\",\"address\":\"0x1234567890123456789012345678901234567890\",\"nonce\":\"0x0\"," +
                "\"yParity\":\"" + yParity + "\",\"r\":\"" + r + "\",\"s\":\"" + s + "\"}";
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_sendUserOperation\",\"params\":[{" + fields + "},\"" +
                          S7R3Host.EntryPoint + "\"]}";

            var (_, body) = await _host.PostAsync(request);

            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.GetProperty("error");
            var message = error.GetProperty("message").GetString();
            Assert.Equal(-32602, error.GetProperty("code").GetInt32());
            Assert.DoesNotContain("Object reference", message);
            if (expectedMessage.Length > 0)
            {
                Assert.Equal(expectedMessage, message);
            }
            else
            {
                Assert.Contains(message, new[] { SignatureInvalid, SignerNotSender });
            }
        }
    }
}
