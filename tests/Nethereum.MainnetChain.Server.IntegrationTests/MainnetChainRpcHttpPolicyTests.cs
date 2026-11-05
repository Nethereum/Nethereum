using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class MainnetChainRpcHttpPolicyTests
    {
        private const string Body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}";

        private static async Task<(WebApplication App, Uri Url)> StartAsync(RpcHttpOptions options)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddCors();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 64 * 1024 * 1024);
            var registry = new RpcHandlerRegistry();
            registry.Register(new EthChainIdHandler());
            builder.Services.AddSingleton(new RpcDispatcher(
                registry, new RpcContext((Func<IChainNode>)(() => null!), 1, null!)));
            builder.Services.AddSingleton(options);

            var app = builder.Build();
            app.MapMainnetChainEndpoints();
            var url = new Uri($"http://127.0.0.1:{port}");
            app.Urls.Add(url.ToString());
            await app.StartAsync();
            return (app, url);
        }

        private static async Task<HttpStatusCode> PostAsync(Uri url, string host, HttpContent content)
        {
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (host != null) request.Headers.Host = host;
            using var response = await http.SendAsync(request);
            return response.StatusCode;
        }

        private static StringContent Json(string body) => new StringContent(body, Encoding.UTF8, "application/json");

        [Fact]
        public async Task Given_TheDefaultHostPolicy_When_AForeignAndALocalHostHeaderArePosted_Then_OnlyTheLocalOneIsServed()
        {
            var (app, url) = await StartAsync(new RpcHttpOptions { BindHost = "127.0.0.1" });
            try
            {
                Assert.Equal(HttpStatusCode.OK, await PostAsync(url, "localhost:8545", Json(Body)));
                Assert.Equal(HttpStatusCode.OK, await PostAsync(url, "10.1.2.3:8545", Json(Body)));
                Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(url, "rpc.example.org", Json(Body)));
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AListedVirtualHost_When_ItsNameIsPosted_Then_ItIsServed()
        {
            var (app, url) = await StartAsync(new RpcHttpOptions
            {
                BindHost = "127.0.0.1",
                AllowedHosts = { "rpc.example.org" }
            });
            try
            {
                Assert.Equal(HttpStatusCode.OK, await PostAsync(url, "rpc.example.org", Json(Body)));
                Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(url, "other.example.org", Json(Body)));
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ABodyLargerThanTheRequestLimit_When_ItIsPosted_Then_TheAnswerIs413()
        {
            var (app, url) = await StartAsync(new RpcHttpOptions { BindHost = "127.0.0.1", MaxRequestBodySize = 1024 });
            try
            {
                var oversized = "[" + string.Join(",", Enumerable.Repeat(Body, 20)) + "]";
                Assert.True(oversized.Length > 1024);

                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await PostAsync(url, null!, Json(oversized)));
                Assert.Equal(HttpStatusCode.OK, await PostAsync(url, null!, Json(Body)));
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }
    }
}
