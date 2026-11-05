using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.DevChain.UnitTests.SecurityAudit71.S14
{
    public class DevChainJsonRpcHttpS14Tests : IAsyncLifetime
    {
        private WebApplication _app;
        private HttpClient _http;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.AddDevChainServer(new DevChainServerConfig { Storage = "memory", Persist = false });
            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
            _app = builder.Build();
            await _app.MapDevChainEndpointsAsync();
            await _app.StartAsync();
            var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First();
            _http = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task DisposeAsync()
        {
            _http.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private Task<HttpResponseMessage> Post(string body) =>
            _http.PostAsync("/", new StringContent(body, Encoding.UTF8, "application/json"));

        [Theory]
        [InlineData(100_000, '[', ']')]
        [InlineData(100_000, '{', '}')]
        [Trait("Rule", "S14-9")]
        public async Task Given_AJsonBodyNested100000Deep_When_Posted_Then_TheServerAnswersAnErrorAndStaysUp(int depth, char open, char close)
        {
            var body = open == '{'
                ? string.Concat(Enumerable.Repeat("{\"a\":", depth)) + "1" + new string('}', depth)
                : new string(open, depth) + new string(close, depth);

            var response = await Post(body);

            Assert.False(response.IsSuccessStatusCode && !(await response.Content.ReadAsStringAsync()).Contains("error"));
            var alive = await Post("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}");
            Assert.True(alive.IsSuccessStatusCode);
        }

        [Fact]
        [Trait("Rule", "S14-9")]
        public async Task Given_AParamsValueNested100000Deep_When_Posted_Then_TheServerAnswersAnErrorAndStaysUp()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getBalance\",\"params\":" +
                       new string('[', 100_000) + new string(']', 100_000) + "}";

            var response = await Post(body);

            Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            var alive = await Post("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}");
            Assert.True(alive.IsSuccessStatusCode);
        }

        [Fact]
        [Trait("Rule", "S14-10")]
        public async Task Given_ABatchOfFiftyThousandRequests_When_Posted_Then_ItIsRejectedByABatchLimit()
        {
            var item = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}";
            var body = "[" + string.Join(",", Enumerable.Repeat(item, 50_000)) + "]";

            var response = await Post(body);
            var text = await response.Content.ReadAsStringAsync();

            var answered = text.StartsWith("[") ? JsonDocument.Parse(text).RootElement.GetArrayLength() : 0;
            Assert.True(answered <= 1000, $"{answered} responses to a 50000 request batch");
        }
    }
}
