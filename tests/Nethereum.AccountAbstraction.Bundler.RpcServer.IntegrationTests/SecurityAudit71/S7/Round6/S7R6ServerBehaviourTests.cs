using System.Net;
using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round6
{
    public class S7R6ServerBehaviourTests : IDisposable
    {
        private const int InvalidRequest = -32600;
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        private const string Call = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_chainId\",\"params\":[]}";

        private const string OpFields =
            "\"sender\":\"0x1111111111111111111111111111111111111111\",\"nonce\":\"0x0\"," +
            "\"callData\":\"0x\",\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0x30d40\"," +
            "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"";

        [Fact]
        [Trait("Rule", "S7-R6-04")]
        [Trait("Erc", "JSON-RPC 2.0 section 4.1 and 5.1")]
        public async Task Post_WhenTheObjectHasNoIdAndNoMethod_AnswersAnInvalidRequestErrorNotNoContent()
        {
            var (status, body) = await _host.PostAsync("{\"jsonrpc\":\"2.0\"}");

            Assert.NotEqual(HttpStatusCode.NoContent, status);
            using var document = JsonDocument.Parse(body);
            Assert.Equal(InvalidRequest, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }

        [Fact]
        [Trait("Rule", "S7-R6-04")]
        [Trait("Erc", "JSON-RPC 2.0 section 4.1 and 5.1")]
        public async Task Post_WhenABatchHoldsACallAndAnObjectWithNoIdAndNoMethod_AnswersAnInvalidRequestErrorForThatMember()
        {
            var (status, body) = await _host.PostAsync("[" + Call + ",{\"jsonrpc\":\"2.0\"}]");

            Assert.Equal(HttpStatusCode.OK, status);
            using var document = JsonDocument.Parse(body);
            Assert.Equal(2, document.RootElement.GetArrayLength());
        }

        [Fact]
        [Trait("Rule", "S7-R6-06")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task SendUserOperation_WhenTheNodeBehindTheBundlerIsUnreachable_AnswersWithoutTheTextOfTheNodeCallException()
        {
            _host.StopNode();
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_sendUserOperation\",\"params\":[{" + OpFields + "},\"" +
                          S7R3Host.EntryPoint + "\"]}";

            var (_, body) = await _host.PostAsync(request);

            using var document = JsonDocument.Parse(body);
            var message = document.RootElement.GetProperty("error").GetProperty("message").GetString() ?? string.Empty;
            Assert.DoesNotContain("Error occurred when trying to send rpc request", message);
        }
    }
}
