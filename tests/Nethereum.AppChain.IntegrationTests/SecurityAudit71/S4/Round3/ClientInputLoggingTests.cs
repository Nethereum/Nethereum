using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class ClientInputLoggingTests
    {
        private readonly ITestOutputHelper _out;

        public ClientInputLoggingTests(ITestOutputHelper output) { _out = output; }

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Theory]
        [InlineData("eth_getBalance", "[\"0xzz\",\"latest\"]")]
        [InlineData("eth_getBlockByNumber", "[\"0xzz\",false]")]
        [InlineData("eth_getTransactionByHash", "[\"0xzz\"]")]
        [Trait("Finding", "S4-R3-01")]
        public async Task Given_AMalformedHexParameter_When_TheRequestIsRejected_Then_NoEntryWithAnExceptionAndStackIsLogged(string method, string parameters)
        {
            await using var host = await DevChainFuzzHost.StartAsync();
            var request = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":" + parameters + "}";

            var body = await host.CallAsync(method, parameters);

            Assert.Equal(-32602, ErrorCode(body));
            List<(LogLevel Level, string Category, string Message, Exception? Exception)> entries;
            lock (host.Log.Entries) entries = host.Log.Entries.ToList();
            var withStack = entries.Where(e => e.Exception != null && e.Level >= LogLevel.Warning).ToList();
            foreach (var entry in withStack)
                _out.WriteLine("request " + request.Length + " bytes -> " + entry.Level + " entry of " + entry.Exception!.ToString().Length + " characters: " + entry.Message);
            Assert.Empty(withStack);
        }

        [Theory]
        [InlineData("eth_call", "[{\"to\":\"0x0000000000000000000000000000000000000001\"},\"latest\",{\"0x0000000000000000000000000000000000000001\":{\"balance\":5}}]")]
        [InlineData("eth_simulateV1", "[{\"blockStateCalls\":[{\"stateOverrides\":{\"0x0000000000000000000000000000000000000001\":{\"nonce\":\"0xffffffffffffffffffffffff\"}},\"calls\":[{\"to\":\"0x0000000000000000000000000000000000000001\"}]}]},\"latest\"]")]
        [InlineData("eth_simulateV1", "[{\"blockStateCalls\":[{\"stateOverrides\":{\"0x0000000000000000000000000000000000000001\":null},\"calls\":[{\"to\":\"0x0000000000000000000000000000000000000001\"}]}]},\"latest\"]")]
        [Trait("Finding", "S4-R3-01")]
        public async Task Given_AWronglyTypedStateOverride_When_TheRequestIsRejected_Then_ItIsInvalidParamsAndNoErrorEntryWithAStackIsLogged(string method, string parameters)
        {
            await using var host = await DevChainFuzzHost.StartAsync();

            var body = await host.CallAsync(method, parameters);

            var code = ErrorCode(body);
            List<(LogLevel Level, string Category, string Message, Exception? Exception)> entries;
            lock (host.Log.Entries) entries = host.Log.Entries.ToList();
            var errors = entries.Where(e => e.Exception != null && e.Level >= LogLevel.Error).ToList();
            foreach (var entry in errors)
                _out.WriteLine(entry.Level + " " + entry.Exception!.GetType().Name + " " + entry.Exception!.ToString().Length + " characters: " + entry.Message);
            Assert.Equal(-32602, code);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("eth_call", "[null]")]
        [InlineData("eth_estimateGas", "[null]")]
        [InlineData("eth_createAccessList", "[null]")]
        [InlineData("eth_simulateV1", "[null]")]
        [InlineData("eth_getFilterChanges", "[null]")]
        [InlineData("eth_getFilterLogs", "[null]")]
        [InlineData("eth_uninstallFilter", "[null]")]
        [Trait("Finding", "S4-R3-01")]
        public async Task Given_ANullParameter_When_TheRequestIsRejected_Then_ItIsInvalidParamsAndNoErrorEntryWithAStackIsLogged(string method, string parameters)
        {
            await using var host = await DevChainFuzzHost.StartAsync();

            var body = await host.CallAsync(method, parameters);

            var code = ErrorCode(body);
            List<(LogLevel Level, string Category, string Message, Exception? Exception)> entries;
            lock (host.Log.Entries) entries = host.Log.Entries.ToList();
            var errors = entries.Where(e => e.Exception != null && e.Level >= LogLevel.Error).ToList();
            foreach (var entry in errors)
                _out.WriteLine(entry.Level + " " + entry.Exception!.GetType().Name + " " + entry.Exception!.ToString().Length + " characters: " + entry.Message);
            Assert.Equal(-32602, code);
            Assert.Empty(errors);
        }
    }
}
