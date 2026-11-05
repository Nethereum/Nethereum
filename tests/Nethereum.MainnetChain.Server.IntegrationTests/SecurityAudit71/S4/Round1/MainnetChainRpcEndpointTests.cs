using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests.SecurityAudit71.S4.Round1
{
    public class MainnetChainRpcEndpointTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"s4r1_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
        }

        private static async Task<(WebApplication App, Uri Url)> StartEndpointsAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
            var registry = new RpcHandlerRegistry();
            registry.Register(new EthChainIdHandler());
            builder.Services.AddSingleton(new RpcDispatcher(
                registry, new RpcContext((Func<IChainNode>)(() => null!), 1, null!)));

            var app = builder.Build();
            app.MapMainnetChainEndpoints();
            var url = new Uri($"http://127.0.0.1:{port}");
            app.Urls.Add(url.ToString());
            await app.StartAsync();
            return (app, url);
        }

        [Fact]
        [Trait("Finding", "S4-R1-03")]
        public async Task Given_ABrowserFormPostWithTextPlainContentType_When_ItIsSentToTheMainnetChainEndpoint_Then_ItIsRefusedAsUnsupportedMediaType()
        {
            var (app, url) = await StartEndpointsAsync();
            try
            {
                using var http = new HttpClient();
                using var response = await http.PostAsync(url,
                    new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "text/plain"));

                Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        [Fact]
        [Trait("Finding", "S4-R1-09")]
        public async Task Given_ABatchOfMillionsOfEmptyMembers_When_ItIsPostedToTheMainnetChainEndpoint_Then_ItIsRejectedWithoutMaterialisingEveryMember()
        {
            var (app, url) = await StartEndpointsAsync();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                var members = 3_000_000;
                var body = "[" + string.Join(",", Enumerable.Repeat("{}", members)) + "]";
                Assert.True(body.Length < 10 * 1024 * 1024);

                GC.Collect();
                var before = GC.GetTotalAllocatedBytes(true);
                using var response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                await response.Content.ReadAsStringAsync();
                var allocated = GC.GetTotalAllocatedBytes(true) - before;

                Assert.True(allocated < 120L * 1024 * 1024,
                    $"a {body.Length / 1024 / 1024} MiB batch of {members} members allocated {allocated / 1024 / 1024} MiB before the batch limit was applied");
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        [Theory]
        [Trait("Finding", "S4-R1-02")]
        [InlineData("debug_traceCall")]
        [InlineData("debug_traceBlockByNumber")]
        [InlineData("debug_traceTransaction")]
        [InlineData("txpool_content")]
        public void Given_ADefaultMainnetChainServer_When_ItsRpcRegistryIsInspected_Then_DebugAndTxpoolMethodsAreNotRegisteredWithoutOptIn(string method)
        {
            var services = new ServiceCollection();
            services.AddMainnetChainServer(new MainnetChainServerConfig());
            using var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<RpcHandlerRegistry>();

            Assert.Null(registry.GetHandler(method));
        }

        [Fact]
        [Trait("Finding", "S4-R1-17")]
        public async Task Given_AnEngineOnlyFollowerWithTheDefaultRpcModules_When_ItsPublicRpcRegistryIsInspected_Then_DebugSetHeadIsNotRegistered()
        {
            await using var server = await EngineOnlyServer.StartAsync(
                _workDir, CustomGenesisFixture.Json(), c => c.RpcModules = Nethereum.CoreChain.Rpc.RpcModuleSet.DefaultModules);

            var registry = server.Services.GetRequiredService<RpcHandlerRegistry>();

            Assert.Null(registry.GetHandler("debug_setHead"));
        }

        [Fact]
        [Trait("Finding", "S4-R1-17")]
        public async Task Given_AnEngineOnlyFollowerWithTheDebugModule_When_ItsPublicRpcRegistryIsInspected_Then_DebugSetHeadIsRegistered()
        {
            await using var server = await EngineOnlyServer.StartAsync(
                _workDir, CustomGenesisFixture.Json(), c => c.RpcModules = "eth,net,web3,debug");

            var registry = server.Services.GetRequiredService<RpcHandlerRegistry>();

            Assert.NotNull(registry.GetHandler("debug_setHead"));
        }

        private static async Task<string?> PreflightAllowedOriginAsync(MainnetChainServerConfig config, string origin)
        {
            var builder = WebApplication.CreateBuilder();
            builder.AddMainnetChainServer(config);
            using var provider = builder.Services.BuildServiceProvider();
            var context = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider };
            context.Request.Method = "OPTIONS";
            context.Request.Headers["Origin"] = origin;
            context.Request.Headers["Access-Control-Request-Method"] = "POST";
            var policy = await provider.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>()
                .GetPolicyAsync(context, null);
            var service = provider.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>();
            service.ApplyResult(service.EvaluatePolicy(context, policy!), context.Response);
            return context.Response.Headers["Access-Control-Allow-Origin"].ToString();
        }

        [Fact]
        [Trait("Finding", "S4-R1-17")]
        public async Task Given_TheDefaultMainnetChainConfig_When_ACrossOriginPreflightIsEvaluated_Then_NoAllowOriginIsGranted()
        {
            var allowed = await PreflightAllowedOriginAsync(new MainnetChainServerConfig(), "http://evil.example");

            Assert.True(string.IsNullOrEmpty(allowed));
        }

        [Fact]
        [Trait("Finding", "S4-R1-17")]
        public async Task Given_ACorsOriginAllowList_When_PreflightsFromListedAndUnlistedOriginsAreEvaluated_Then_OnlyTheListedOneIsGranted()
        {
            var config = new MainnetChainServerConfig { RpcCorsOrigins = "http://dapp.example" };

            var listed = await PreflightAllowedOriginAsync(config, "http://dapp.example");
            var unlisted = await PreflightAllowedOriginAsync(config, "http://evil.example");

            Assert.Equal("http://dapp.example", listed);
            Assert.True(string.IsNullOrEmpty(unlisted));
        }
    }
}
