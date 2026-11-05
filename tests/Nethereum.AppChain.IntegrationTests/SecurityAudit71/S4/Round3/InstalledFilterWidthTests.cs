using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class InstalledFilterWidthTests
    {
        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        [Theory]
        [InlineData("eth_getFilterLogs")]
        [InlineData("eth_getFilterChanges")]
        [Trait("Finding", "S4-R3-04")]
        public async Task Given_AWideLogFilterAlreadyInTheFilterStore_When_ItIsPolled_Then_ItIsRefusedAsInvalidParams(string method)
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            var filter = new LogFilter { FromBlock = 0, ToBlock = 0 };
            for (var i = 0; i < 5000; i++) filter.Addresses.Add("0x" + (0x4000000000000000UL + (ulong)i).ToString("x16").PadLeft(40, '0'));
            var id = host.Services.GetRequiredService<IFilterStore>().CreateLogFilter(filter, 0);

            var body = await host.CallAsync(method, "[\"" + id + "\"]");

            Assert.Equal(-32602, ErrorCode(body));
        }
    }
}
