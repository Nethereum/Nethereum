using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    internal sealed class EngineOnlyServer : IAsyncDisposable
    {
        private readonly IHost _host;

        private EngineOnlyServer(IHost host, MainnetChainServerConfig config)
        {
            _host = host;
            Config = config;
        }

        public MainnetChainServerConfig Config { get; }

        public IServiceProvider Services => _host.Services;

        public IChainStoreBundle Bundle => Services.GetRequiredService<IChainStoreBundle>();

        public AsyncGate Gate => Services.GetRequiredService<AsyncGate>();

        public Nethereum.CoreChain.Engine.FollowerEngineApiService Engine =>
            Services.GetRequiredService<Nethereum.CoreChain.Engine.FollowerEngineApiService>();

        public static async Task<EngineOnlyServer> StartAsync(
            string dataDir, string genesisJson, Action<MainnetChainServerConfig>? configure = null)
        {
            Directory.CreateDirectory(dataDir);
            var genesisPath = Path.Combine(dataDir, "genesis.json");
            File.WriteAllText(genesisPath, genesisJson);

            var config = new MainnetChainServerConfig
            {
                DataDir = Path.Combine(dataDir, "chaindata"),
                GenesisPath = genesisPath,
            };
            config.Engine.Enabled = true;
            config.RpcModules = "eth,net,web3,debug";
            config.Engine.JwtSecretPath = Path.Combine(dataDir, "jwt.hex");
            configure?.Invoke(config);

            var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Services.AddMainnetChainServer(config);
            await builder.Services.AddMainnetNodeAsync(config, loggerFactory);
            var host = builder.Build();
            await host.StartAsync();

            var server = new EngineOnlyServer(host, config);
            await server.WaitForFollowerAsync();
            return server;
        }

        private async Task WaitForFollowerAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (Gate.Executor != null && Gate.HasHead) return;
                await Task.Delay(20);
            }
            throw new TimeoutException("the engine-only follower never published its executor and head");
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
