using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class EngineApiHttpFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private Task? _runTask;

        public const int Port = 18560;
        public const int EnginePort = 18561;
        public const int ChainId = 31337;

        public string Url => $"http://127.0.0.1:{Port}";
        public string EngineUrl => $"http://127.0.0.1:{EnginePort}";

        public string JwtSecretPath { get; } =
            Path.Combine(Path.GetTempPath(), $"engine-api-jwt-{Guid.NewGuid():N}.hex");

        public async Task InitializeAsync()
        {
            var config = new DevChainServerConfig
            {
                Port = Port,
                ChainId = ChainId,
                Hardfork = "cancun",
                Storage = "memory",
                AutoMine = false,
                EngineApiEnabled = true,
                EngineJwtSecretPath = JwtSecretPath,
                EnginePort = EnginePort
            };

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddDevChainServer(config);
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
            builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

            _app = builder.Build();
            _app.Urls.Add(Url);
            await _app.MapDevChainEndpointsAsync();

            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync(); }
                catch (OperationCanceledException) { }
            });

            await WaitForServerReadyAsync();
        }

        public async Task DisposeAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            if (_runTask != null)
            {
                try { await _runTask; }
                catch (OperationCanceledException) { }
            }

            try { File.Delete(JwtSecretPath); }
            catch (IOException) { }
        }

        private async Task WaitForServerReadyAsync(int maxRetries = 50)
        {
            using var client = new HttpClient();
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[],\"id\":1}",
                        Encoding.UTF8,
                        "application/json");
                    var response = await client.PostAsync(Url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }

            throw new Exception($"Server did not become ready at {Url}");
        }

        public string MintValidToken(long? iat = null)
        {
            var secretHex = File.ReadAllText(JwtSecretPath).Trim();
            var secret = Convert.FromHexString(secretHex);
            return MintToken(secret, iat ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public static string MintToken(byte[] secret, long iat)
        {
            var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
            var payloadJson = JsonSerializer.Serialize(new { iat });

            var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            using var hmac = new HMACSHA256(secret);
            var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(headerB64 + "." + payloadB64));
            var signatureB64 = Base64UrlEncode(signature);

            return headerB64 + "." + payloadB64 + "." + signatureB64;
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
