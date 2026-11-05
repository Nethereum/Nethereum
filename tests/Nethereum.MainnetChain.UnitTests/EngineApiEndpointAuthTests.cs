using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.EngineApi.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class EngineApiEndpointAuthTests
    {
        private static string Token(byte[] secret)
        {
            static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = B64(Encoding.UTF8.GetBytes("{\"iat\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "}"));
            using var hmac = new HMACSHA256(secret);
            return header + "." + payload + "." + B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<(HttpResponseMessage Response, string Body)> SendAsync(HttpClient http, int port, HttpMethod method, string? token)
        {
            using var request = new HttpRequestMessage(method, $"http://127.0.0.1:{port}/");
            if (method == HttpMethod.Post) request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await http.SendAsync(request);
            return (response, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Given_the_engine_endpoint_When_the_token_is_missing_or_invalid_or_the_method_is_not_post_Then_it_answers_401_or_404()
        {
            var jwtPath = Path.Combine(Path.GetTempPath(), $"engine-auth-{Guid.NewGuid():N}.hex");
            var port = FreePort();
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.Services.AddSingleton(sp => new RpcContext(() => null!, BigInteger.One, sp));
                builder.AddEngineApiServer(
                    new EngineApiServerConfig { Port = port, BindAddress = "127.0.0.1", JwtSecretPath = jwtPath },
                    provider => DispatchProxy.Create<IEngineApiService, EngineApiHostingReadmeExampleTests.NoOpEngineService>());
                await using var app = builder.Build();
                app.MapEngineApiEndpoints();
                await app.StartAsync();

                var secret = Convert.FromHexString(File.ReadAllText(jwtPath).Trim());
                using var http = new HttpClient();

                var missing = await SendAsync(http, port, HttpMethod.Post, null);
                Assert.Equal(HttpStatusCode.Unauthorized, missing.Response.StatusCode);
                Assert.Equal("{\"error\":\"unauthorized\"}", missing.Body);

                var invalid = await SendAsync(http, port, HttpMethod.Post, Token(new byte[32]));
                Assert.Equal(HttpStatusCode.Unauthorized, invalid.Response.StatusCode);
                Assert.Equal("{\"error\":\"unauthorized\"}", invalid.Body);

                var notPost = await SendAsync(http, port, HttpMethod.Get, Token(secret));
                Assert.Equal(HttpStatusCode.NotFound, notPost.Response.StatusCode);

                await app.StopAsync();
            }
            finally
            {
                if (File.Exists(jwtPath)) File.Delete(jwtPath);
            }
        }
    }
}
