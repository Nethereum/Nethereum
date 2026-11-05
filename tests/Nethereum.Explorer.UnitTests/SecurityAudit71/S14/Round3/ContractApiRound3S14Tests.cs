using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIRepository;
using Nethereum.Explorer.Services;
using Xunit;

namespace Nethereum.Explorer.UnitTests.SecurityAudit71.S14.Round3
{
    public class ContractApiRound3S14Tests
    {
        private const string Abi = "[{\"type\":\"function\",\"name\":\"f\",\"inputs\":[],\"outputs\":[],\"stateMutability\":\"view\"}]";
        private const string Address = "0x00000000000000000000000000000000000000aa";
        private const string Key = "Secret-Key-1234";

        private sealed class RecordingAbiStorage : IAbiStorageService
        {
            public ConcurrentDictionary<string, string> Stored { get; } = new();

            public Task<ABIInfo?> GetContractAbiAsync(string contractAddress) => Task.FromResult<ABIInfo?>(null);

            public Task StoreAbiAsync(string contractAddress, string abi, string? name = null, AbiSource source = AbiSource.LocalUpload)
            {
                Stored[contractAddress.ToLowerInvariant()] = abi;
                return Task.CompletedTask;
            }
        }

        private sealed class Host : IAsyncDisposable
        {
            public WebApplication App = null!;
            public HttpClient Client = null!;
            public RecordingAbiStorage Storage = new();

            public static async Task<Host> StartAsync(string? apiKey, bool rateLimit)
            {
                var host = new Host();
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Services.AddSingleton<IAbiStorageService>(host.Storage);
                builder.Services.AddSingleton<IOptions<ExplorerOptions>>(Options.Create(new ExplorerOptions { ApiKey = apiKey }));
                if (rateLimit) builder.Services.AddRateLimiter(o => o.ConfigureApiRateLimit());
                host.App = builder.Build();
                if (rateLimit) host.App.UseRateLimiter();
                host.App.MapContractApiEndpoints();
                await host.App.StartAsync();
                var address = host.App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
                host.Client = new HttpClient { BaseAddress = new Uri(address) };
                return host;
            }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static HttpRequestMessage Post(string url, HttpContent content, string? key)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (key != null) request.Headers.Add("X-Api-Key", key);
            return request;
        }

        private static StringContent Json(string body) => new StringContent(body, Encoding.UTF8, "application/json");

        [Fact]
        [Trait("Rule", "S14-R3-7")]
        public async Task Given_NoValidKey_When_TheBodyIsInvalidHugeOrNotJson_Then_TheAnswerIs401BeforeTheBodyIsEverRead()
        {
            await using var host = await Host.StartAsync(Key, false);
            var deep = new string('[', 200_000) + new string(']', 200_000);
            var big = "{\"abi\":\"" + new string('a', 5_000_000) + "\"}";

            foreach (var content in new HttpContent[]
                     {
                         Json("{ not json"), Json(deep), Json(big),
                         new StringContent("plain", Encoding.UTF8, "text/plain"),
                         new ByteArrayContent(new byte[1024])
                     })
            {
                var response = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", content, "wrong"));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            Assert.Empty(host.Storage.Stored);
        }

        [Fact]
        [Trait("Rule", "S14-R3-7")]
        public async Task Given_TheValidKey_When_TheBodyIsNotJsonOrIsMalformedOrDeep_Then_415Or400AndNothingIsStored()
        {
            await using var host = await Host.StartAsync(Key, false);

            var notJson = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi",
                new StringContent("{\"abi\":\"x\"}", Encoding.UTF8, "text/plain"), Key));
            var noContentType = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi",
                new ByteArrayContent(Encoding.UTF8.GetBytes("{\"abi\":\"x\"}")), Key));
            var malformed = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", Json("{ not json"), Key));
            var deep = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi",
                Json(new string('[', 200_000) + new string(']', 200_000)), Key));
            var nullBody = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", Json("null"), Key));
            var batchMalformed = await host.Client.SendAsync(Post("/api/contracts/batch", Json("{\"contracts\":[{"), Key));
            var batchWrongShape = await host.Client.SendAsync(Post("/api/contracts/batch", Json("{\"contracts\":5}"), Key));

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJson.StatusCode);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, noContentType.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, deep.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, nullBody.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, batchMalformed.StatusCode);
            Assert.True(batchWrongShape.StatusCode == HttpStatusCode.BadRequest, batchWrongShape.StatusCode.ToString());
            Assert.Empty(host.Storage.Stored);
        }

        [Fact]
        [Trait("Rule", "S14-R3-7")]
        public async Task Given_TheValidKey_When_AValidJsonVariantIsPosted_Then_ItIsStored()
        {
            await using var host = await Host.StartAsync(Key, false);
            var body = "{\"abi\":" + JsonSerializer.Serialize(Abi) + "}";
            var charset = new StringContent(body, Encoding.UTF8, "application/json");
            charset.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

            var response = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", charset, Key));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(host.Storage.Stored);
        }

        [Fact]
        [Trait("Rule", "S14-R3-8")]
        public async Task Given_ForwardedForHeadersThatDiffer_When_MoreThanTheLimitIsSentFromOneAddress_Then_TheLimiterStillRejects()
        {
            await using var host = await Host.StartAsync(Key, true);
            var rejected = 0;

            for (var i = 0; i < 130; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/contracts/" + Address + "/abi");
                request.Headers.Add("X-Forwarded-For", "10.0.0." + (i % 250) + ", 203.0.113." + (i % 200));
                request.Headers.Add("Forwarded", "for=198.51.100." + (i % 200));
                request.Headers.Add("X-Real-IP", "192.0.2." + (i % 200));
                var response = await host.Client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.TooManyRequests) rejected++;
            }

            Assert.True(rejected >= 20, $"{rejected} of 130 were rejected");
        }

        [Fact]
        [Trait("Rule", "S14-R3-8")]
        public async Task Given_TheLimiterIsExhausted_When_AnUnauthenticatedPostArrives_Then_NothingIsStoredAndTheKeyedPostIsAlsoLimited()
        {
            await using var host = await Host.StartAsync(Key, true);
            for (var i = 0; i < 100; i++)
                await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", Json("{}"), "wrong"));

            var limited = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi",
                Json("{\"abi\":" + JsonSerializer.Serialize(Abi) + "}"), Key));

            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.Empty(host.Storage.Stored);
        }
    }
}
