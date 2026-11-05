using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round7
{
    public class S7R7HostedProbeTests : IDisposable
    {
        private const int InvalidParams = -32602;
        private const int InvalidRequest = -32600;
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        private static int? ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetProperty("code").GetInt32()
                : null;
        }

        [Theory]
        [InlineData("eth_getUserOperationReceipt", "\"\\ud800\"")]
        [InlineData("eth_getUserOperationByHash", "\"\\ud800\"")]
        [InlineData("eth_estimateUserOperationGas", "{\"sender\":\"\\ud800\"},\"" + S7R3Host.EntryPoint + "\"")]
        [InlineData("eth_sendUserOperation", "{\"sender\":\"\\ud800\"},\"" + S7R3Host.EntryPoint + "\"")]
        [InlineData("eth_sendUserOperation", "{},\"\\ud800\"")]
        [Trait("Rule", "S7-R7-08")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Post_WhenAStringParameterHoldsALoneSurrogateEscape_AnswersInvalidParamsNotAnInternalError(string method, string parameters)
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":[" + parameters + "]}";

            var (_, response) = await _host.PostAsync(body);

            Assert.Equal(InvalidParams, ErrorCode(response));
        }

        [Fact]
        [Trait("Rule", "S7-R7-09")]
        [Trait("Erc", "JSON-RPC 2.0 section 4")]
        public async Task Post_WhenTheMethodMemberIsWrittenWithOtherCasing_IsAnInvalidRequest()
        {
            var (_, response) = await _host.PostAsync("{\"jsonrpc\":\"2.0\",\"id\":7,\"METHOD\":\"eth_chainId\",\"params\":[]}");

            Assert.Equal(InvalidRequest, ErrorCode(response));
        }
    }
}
