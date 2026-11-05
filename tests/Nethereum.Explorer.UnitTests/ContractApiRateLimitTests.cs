using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIRepository;
using Nethereum.Explorer.Services;
using Xunit;

namespace Nethereum.Explorer.UnitTests
{
    public class ContractApiRateLimitTests
    {
        private sealed class EmptyAbiStorage : IAbiStorageService
        {
            public Task<ABIInfo?> GetContractAbiAsync(string contractAddress) => Task.FromResult<ABIInfo?>(null);

            public Task StoreAbiAsync(string contractAddress, string abi, string? name = null, AbiSource source = AbiSource.LocalUpload) =>
                Task.CompletedTask;
        }

        private static HttpClient ClientFrom(string localAddress, string baseAddress)
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(new IPEndPoint(IPAddress.Parse(localAddress), 0));
                    await socket.ConnectAsync(context.DnsEndPoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
            return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        }

        [Fact]
        public async Task Given_TwoClientAddresses_When_OneExhaustsItsQuota_Then_TheOtherIsStillServed()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<IAbiStorageService>(new EmptyAbiStorage());
            builder.Services.AddSingleton<IOptions<ExplorerOptions>>(Options.Create(new ExplorerOptions()));
            builder.Services.AddRateLimiter(options => options.ConfigureApiRateLimit());
            await using var app = builder.Build();
            app.UseRateLimiter();
            app.MapContractApiEndpoints();
            await app.StartAsync();
            var baseAddress = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var url = "/api/contracts/0x00000000000000000000000000000000000000aa/abi";

            using var first = ClientFrom("127.0.0.1", baseAddress);
            using var second = ClientFrom("127.0.0.2", baseAddress);

            for (var i = 0; i < 100; i++)
                Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync(url)).StatusCode);
            var exhausted = await first.GetAsync(url);
            var other = await second.GetAsync(url);
            await app.StopAsync();

            Assert.Equal((HttpStatusCode)429, exhausted.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        }
    }
}
