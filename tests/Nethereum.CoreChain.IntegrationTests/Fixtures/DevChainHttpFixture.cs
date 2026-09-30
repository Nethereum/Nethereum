using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class DevChainHttpFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private Task? _runTask;

        public const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string Address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public const int ChainId = 31337;
        public const int Port = 18545;
        public virtual int ServerPort => Port;
        public string Url => $"http://127.0.0.1:{ServerPort}";

        public const string RecipientAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        public Nethereum.Web3.Web3 Web3 { get; private set; } = null!;
        public Nethereum.Web3.Accounts.Account Account { get; private set; } = null!;

        protected virtual DevChainServerConfig CreateConfig() => new DevChainServerConfig
        {
            Port = ServerPort,
            ChainId = ChainId
        };

        public async Task InitializeAsync()
        {
            var config = CreateConfig();

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddDevChainServer(config);
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
            builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

            _app = builder.Build();

            await _app.MapDevChainEndpointsAsync();

            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync(Url); }
                catch (OperationCanceledException) { }
            });

            await WaitForServerReadyAsync();

            Account = new Nethereum.Web3.Accounts.Account(PrivateKey, ChainId);
            Account.TransactionManager.UseLegacyAsDefault = true;
            Web3 = new Nethereum.Web3.Web3(Account, Url);
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
        }

        private async Task WaitForServerReadyAsync(int maxRetries = 50)
        {
            using var client = new HttpClient();
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[],\"id\":1}",
                        System.Text.Encoding.UTF8,
                        "application/json");
                    var response = await client.PostAsync(Url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(100);
            }
            throw new Exception($"Server did not become ready at {Url}");
        }

    }
}
