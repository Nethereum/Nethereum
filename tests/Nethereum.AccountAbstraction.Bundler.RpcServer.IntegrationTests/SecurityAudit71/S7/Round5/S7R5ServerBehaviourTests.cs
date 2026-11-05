using System.Net;
using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round5
{
    public class S7R5ServerBehaviourTests : IDisposable
    {
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        private const string Notification = "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[]}";
        private const string Call = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_chainId\",\"params\":[]}";

        [Fact]
        [Trait("Rule", "S7-R5-10")]
        [Trait("Erc", "JSON-RPC 2.0 notifications")]
        public async Task Post_WhenTheRequestIsANotification_AnswersNoContentWithNoBody()
        {
            var (status, body) = await _host.PostAsync(Notification);

            Assert.Equal(HttpStatusCode.NoContent, status);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        [Trait("Rule", "S7-R5-10")]
        [Trait("Erc", "JSON-RPC 2.0 notifications")]
        public async Task Post_WhenABatchHoldsANotificationAndACall_AnswersOnlyTheCall()
        {
            var (status, body) = await _host.PostAsync("[" + Notification + "," + Call + "]");

            Assert.Equal(HttpStatusCode.OK, status);
            using var document = JsonDocument.Parse(body);
            Assert.Equal(1, document.RootElement.GetArrayLength());
            Assert.Equal(7, document.RootElement[0].GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Rule", "S7-R5-10")]
        [Trait("Erc", "JSON-RPC 2.0 notifications")]
        public async Task Post_WhenABatchHoldsOnlyNotifications_AnswersNoContentWithNoBody()
        {
            var (status, body) = await _host.PostAsync("[" + Notification + "," + Notification + "]");

            Assert.Equal(HttpStatusCode.NoContent, status);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        [Trait("Rule", "S7-R5-09")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Post_WhenTheNodeBehindTheBundlerIsUnreachable_AnswersAnInternalErrorWithoutTheExceptionMessage()
        {
            _host.StopNode();
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getUserOperationReceipt\",\"params\":[\"0x" + new string('c', 64) + "\"]}";

            var (_, body) = await _host.PostAsync(request);

            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.GetProperty("error");
            Assert.Equal(-32603, error.GetProperty("code").GetInt32());
            Assert.Equal("Internal error", error.GetProperty("message").GetString());
        }
    }
}
