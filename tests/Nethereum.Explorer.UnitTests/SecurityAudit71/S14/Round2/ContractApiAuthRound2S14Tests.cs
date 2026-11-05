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

namespace Nethereum.Explorer.UnitTests.SecurityAudit71.S14.Round2
{
    public class ContractApiAuthRound2S14Tests
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

            public static async Task<Host> StartAsync(string? apiKey)
            {
                var host = new Host();
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Services.AddSingleton<IAbiStorageService>(host.Storage);
                builder.Services.AddSingleton<IOptions<ExplorerOptions>>(Options.Create(new ExplorerOptions { ApiKey = apiKey }));
                host.App = builder.Build();
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

        private static StringContent Body() =>
            new StringContent("{\"abi\":" + JsonSerializer.Serialize(Abi) + ",\"name\":\"n\"}", Encoding.UTF8, "application/json");

        private static HttpRequestMessage Request(HttpMethod method, string url, params string[] keys)
        {
            var request = new HttpRequestMessage(method, url) { Content = Body() };
            if (keys.Length > 0) request.Headers.Add("X-Api-Key", keys);
            return request;
        }

        public static TheoryData<string[]> NonMatchingKeys() => new TheoryData<string[]>
        {
            new[] { "Secret-Key-123" },
            new[] { "Secret-Key-12345" },
            new[] { "secret-key-1234" },
            new[] { "wrong", Key },
            new[] { Key + "x", Key },
            new[] { "" }
        };

        [Theory]
        [MemberData(nameof(NonMatchingKeys))]
        [Trait("Rule", "S14-R2-8")]
        public async Task Given_AnApiKey_When_AnyNonExactHeaderShapeIsSent_Then_401AndNothingIsStored(string[] headerValues)
        {
            await using var host = await Host.StartAsync(Key);

            var single = await host.Client.SendAsync(Request(HttpMethod.Post, "/api/contracts/" + Address + "/abi", headerValues));

            Assert.Equal(HttpStatusCode.Unauthorized, single.StatusCode);
            Assert.Empty(host.Storage.Stored);
        }

        [Theory]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        [InlineData("OPTIONS")]
        [Trait("Rule", "S14-R2-8")]
        public async Task Given_NoValidKey_When_OtherVerbsHitTheUploadRoutes_Then_NothingIsStored(string verb)
        {
            await using var host = await Host.StartAsync(Key);

            await host.Client.SendAsync(Request(new HttpMethod(verb), "/api/contracts/" + Address + "/abi"));
            await host.Client.SendAsync(Request(new HttpMethod(verb), "/api/contracts/batch"));
            await host.Client.SendAsync(Request(HttpMethod.Get, "/api/contracts/" + Address + "/abi"));

            Assert.Empty(host.Storage.Stored);
        }

        [Fact]
        [Trait("Rule", "S14-R2-8")]
        public async Task Given_TheExactKey_When_UploadingADeepAbi_Then_ItIsRefusedAndNothingIsStored()
        {
            await using var host = await Host.StartAsync(Key);
            var deep = "function f(" + string.Concat(Enumerable.Repeat("(", 5000)) + "uint256" + string.Concat(Enumerable.Repeat(")", 5000)) + ")";
            var content = new StringContent("{\"abi\":" + JsonSerializer.Serialize(deep) + "}", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/contracts/" + Address + "/abi") { Content = content };
            request.Headers.Add("X-Api-Key", Key);

            var response = await host.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(host.Storage.Stored);
        }
    }
}
