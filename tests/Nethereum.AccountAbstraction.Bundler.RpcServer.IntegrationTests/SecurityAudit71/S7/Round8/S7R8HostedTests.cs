using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round8
{
    public class S7R8HostedTests
    {
        private const string Escape = "\\" + "ud800";
        private const int ParseError = -32700;
        private const int InvalidParams = -32602;

        private static int? ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetProperty("code").GetInt32()
                : null;
        }

        [Fact]
        [Trait("Rule", "S7-R8-01")]
        [Trait("Erc", "JSON-RPC 2.0 section 5")]
        public async Task Post_WhenTheRequestIdHoldsALoneSurrogateEscape_DoesNotAnswerAParseError()
        {
            using var host = new S7R3Host();

            var (_, body) = await host.PostAsync("{\"jsonrpc\":\"2.0\",\"id\":\"" + Escape + "\",\"method\":\"eth_chainId\",\"params\":[]}");

            Assert.NotEqual(ParseError, ErrorCode(body));
        }

        [Fact]
        [Trait("Rule", "S7-R8-01")]
        [Trait("Erc", "JSON-RPC 2.0 section 5")]
        public async Task Post_WhenOneBatchMemberHasALoneSurrogateEscapeId_TheOtherMembersStillGetTheirResponses()
        {
            using var host = new S7R3Host();

            var (_, body) = await host.PostAsync(
                "[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}," +
                "{\"jsonrpc\":\"2.0\",\"id\":\"" + Escape + "\",\"method\":\"eth_chainId\",\"params\":[]}]");

            using var document = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        }

        [Fact]
        [Trait("Rule", "S7-R8-01")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task Post_WhenVerboseLoggingIsOnAndAParamHoldsALoneSurrogateEscape_AnswersInvalidParams()
        {
            using var host = new S7R3Host("--verbose=true --Logging:LogLevel:Default=Debug");

            var (_, body) = await host.PostAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getUserOperationReceipt\",\"params\":[\"" + Escape + "\"]}");

            Assert.Equal(InvalidParams, ErrorCode(body));
        }
    }
}
