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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIRepository;
using Nethereum.Explorer.Services;
using Xunit;

namespace Nethereum.Explorer.UnitTests
{
    public class ContractApiUploadAuthTests
    {
        private const string Abi = "[{\"type\":\"function\",\"name\":\"f\",\"inputs\":[],\"outputs\":[],\"stateMutability\":\"view\"}]";
        private const string Address = "0x00000000000000000000000000000000000000aa";
        private const string OtherAddress = "0x00000000000000000000000000000000000000bb";

        private sealed class RecordingAbiStorage : IAbiStorageService
        {
            public ConcurrentDictionary<string, string> Stored { get; } = new();

            public Task<ABIInfo?> GetContractAbiAsync(string contractAddress)
            {
                if (!Stored.TryGetValue(contractAddress.ToLowerInvariant(), out var abi))
                    return Task.FromResult<ABIInfo?>(null);
                return Task.FromResult<ABIInfo?>(new ABIInfo { ABI = abi, ContractName = "n" });
            }

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

        private static StringContent SingleBody() =>
            new StringContent("{\"abi\":" + JsonSerializer.Serialize(Abi) + ",\"name\":\"n\"}", Encoding.UTF8, "application/json");

        private static StringContent BatchBody(string address) =>
            new StringContent("{\"contracts\":[{\"address\":\"" + address + "\",\"abi\":" + JsonSerializer.Serialize(Abi) + "}]}", Encoding.UTF8, "application/json");

        private static HttpRequestMessage Post(string url, HttpContent body, string? key)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = body };
            if (key != null) request.Headers.Add("X-Api-Key", key);
            return request;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Given_NoApiKeyConfigured_When_UploadingAbi_Then_401AndNothingStored(string? configured)
        {
            await using var host = await Host.StartAsync(configured);

            var single = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", SingleBody(), null));
            var withEmptyHeader = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", SingleBody(), ""));
            var batch = await host.Client.SendAsync(Post("/api/contracts/batch", BatchBody(Address), null));

            Assert.Equal(HttpStatusCode.Unauthorized, single.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, withEmptyHeader.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, batch.StatusCode);
            Assert.Empty(host.Storage.Stored);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("wrong")]
        public async Task Given_ApiKeyConfigured_When_HeaderMissingOrWrong_Then_401AndNothingStored(string? header)
        {
            await using var host = await Host.StartAsync("secret");

            var single = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", SingleBody(), header));
            var batch = await host.Client.SendAsync(Post("/api/contracts/batch", BatchBody(Address), header));

            Assert.Equal(HttpStatusCode.Unauthorized, single.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, batch.StatusCode);
            Assert.Empty(host.Storage.Stored);
        }

        [Fact]
        public async Task Given_ApiKeyConfigured_When_CorrectHeader_Then_AbiIsStored()
        {
            await using var host = await Host.StartAsync("secret");

            var single = await host.Client.SendAsync(Post("/api/contracts/" + Address + "/abi", SingleBody(), "secret"));
            Assert.Equal(HttpStatusCode.OK, single.StatusCode);
            Assert.Single(host.Storage.Stored);

            var batch = await host.Client.SendAsync(Post("/api/contracts/batch", BatchBody(OtherAddress), "secret"));
            Assert.Equal(HttpStatusCode.OK, batch.StatusCode);
            Assert.Equal(2, host.Storage.Stored.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public async Task Given_AnyApiKeyConfiguration_When_GettingAbi_Then_NoKeyIsRequired(string? configured)
        {
            await using var host = await Host.StartAsync(configured);
            host.Storage.Stored[Address] = Abi;

            var found = await host.Client.GetAsync("/api/contracts/" + Address + "/abi");
            var missing = await host.Client.GetAsync("/api/contracts/" + OtherAddress + "/abi");

            Assert.Equal(HttpStatusCode.OK, found.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }
}
