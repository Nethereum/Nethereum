using System.Net;
using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round4
{
    public class S7R4HostedTests : IDisposable
    {
        private const int InvalidRequest = -32600;
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        [Fact]
        [Trait("Rule", "S7-R4-06")]
        [Trait("Erc", "JSON-RPC batch")]
        public async Task Post_WhenOneBatchMemberHasAWronglyTypedField_AnswersThatMemberAsInvalidRequestAndStillAnswersTheOthers()
        {
            var good = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}";
            var bad = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":5,\"params\":[]}";

            var (status, body) = await _host.PostAsync("[" + good + "," + bad + "]");

            Assert.Equal(HttpStatusCode.OK, status);
            using var document = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(2, document.RootElement.GetArrayLength());
            Assert.Equal(InvalidRequest, document.RootElement[1].GetProperty("error").GetProperty("code").GetInt32());
        }
    }
}
