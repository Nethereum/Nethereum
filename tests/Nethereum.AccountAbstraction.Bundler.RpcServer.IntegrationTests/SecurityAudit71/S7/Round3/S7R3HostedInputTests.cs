using System.Text.Json;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3
{
    public class S7R3HostedInputTests : IDisposable
    {
        private const int InvalidParams = -32602;
        private readonly S7R3Host _host = new();

        public void Dispose() => _host.Dispose();

        private const string Sender = "\"sender\":\"0x1111111111111111111111111111111111111111\"";
        private const string Rest =
            "\"nonce\":\"0x0\",\"callGasLimit\":\"0x186a0\",\"verificationGasLimit\":\"0x249f0\",\"preVerificationGas\":\"0xc350\"," +
            "\"maxFeePerGas\":\"0x77359400\",\"maxPriorityFeePerGas\":\"0x3b9aca00\",\"signature\":\"0x\"";

        private static string Send(string userOpFields) =>
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_sendUserOperation\",\"params\":[{" + userOpFields + "},\"" +
            S7R3Host.EntryPoint + "\"]}";

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Theory]
        [InlineData("\"callData\":\"0x\",\"callData\":\"0xzz\"")]
        [InlineData("\"callData\":\"0x\",\"CALLDATA\":\"0xzz\"")]
        [Trait("Rule", "S7-R3-06")]
        [Trait("Erc", "ERC-4337 input validation")]
        public async Task SendUserOperation_WhenAFieldAppearsTwiceInTheUserOperation_IsRejectedAsInvalidParams(string callDataFields)
        {
            var (_, body) = await _host.PostAsync(Send(Sender + "," + callDataFields + "," + Rest));

            Assert.Equal(InvalidParams, ErrorCode(body));
        }

        [Fact]
        [Trait("Rule", "S7-R3-06")]
        [Trait("Erc", "ERC-4337 input validation")]
        public async Task SendUserOperation_WhenAFieldNameIsNotInTheSpecifiedCase_IsRejectedAsInvalidParams()
        {
            var (_, body) = await _host.PostAsync(Send("\"Sender\":\"0x1111111111111111111111111111111111111111\",\"callData\":\"0x\"," + Rest));

            Assert.Equal(InvalidParams, ErrorCode(body));
        }

        [Fact]
        [Trait("Rule", "S7-R3-11")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Post_BatchOfMillionsOfEmptyMembersWithinTheBodyLimit_DoesNotMultiplyTheServerMemoryByTheBodySize()
        {
            var baseline = _host.PeakWorkingSetBytes;
            var body = new System.Text.StringBuilder("[");
            var members = (5 * 1024 * 1024 - 10) / 3;
            for (var i = 0; i < members; i++) body.Append("{},");
            body.Length--;
            body.Append(']');

            var (status, _) = await _host.PostAsync(body.ToString());
            var growth = _host.PeakWorkingSetBytes - baseline;

            Assert.Equal(System.Net.HttpStatusCode.OK, status);
            Assert.True(growth < 200L * 1024 * 1024, $"a {body.Length} byte batch grew the server peak working set by {growth / 1024 / 1024} MiB");
        }
    }
}
