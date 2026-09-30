using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.EVM.Precompiles.Bls;

namespace Nethereum.Node.HarnessServer
{
    public static class HarnessNodeHost
    {
        public static async Task<WebApplication> BuildAsync(HarnessServerOptions options, string[] args)
        {
            var document = StandardGenesisLoader.LoadFromFile(options.GenesisPath);

            var builder = WebApplication.CreateBuilder(args);

            var config = new DevChainServerConfig
            {
                Host = options.HttpAddr,
                Port = options.HttpPort,
                DataDir = options.DataDir,
                Storage = "memory",
                Persist = true,
                Verbose = options.Verbose,
                EngineApiEnabled = true,
                EngineJwtSecretPath = options.JwtSecretPath ?? Path.Combine(options.DataDir, "jwt.hex"),
                EnginePort = options.AuthRpcPort,
                EngineBindAddress = options.AuthRpcAddr,
            };

            var liveChainConfig = config.GetLiveChainConfig();
            StandardGenesisLoader.ApplyToChainConfig(liveChainConfig, document);
            liveChainConfig.Registry = Bls12381AwareMainnetHardforkRegistry.Build(
                Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
                new Nethereum.Signer.Bls.Herumi.Bls12381Operations());
            liveChainConfig.RewardPolicy = EthereumProofOfWorkRewardPolicy.Instance;

            builder.Services.AddDevChainServer(config);
            builder.Services.AddCors(o => o.AddDefaultPolicy(
                p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
            builder.Logging.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

            var app = builder.Build();
            app.Urls.Add($"http://{options.HttpAddr}:{options.HttpPort}");

            var importSummary = RlpBlockImportSummary.Empty;
            await app.MapDevChainEndpointsAsync(async node =>
            {
                await StandardGenesisLoader.PopulateAllocAsync(node.State, document.Alloc);
                await node.StartAsync();

                var bundle = app.Services.GetRequiredService<IChainStoreBundle>();
                var importer = new BlockImporter(
                    node.BlockManager.Engine, node.Blocks, node.State,
                    node.Transactions, node.Receipts, node.Logs,
                    uncleStore: bundle.Uncles,
                    logger: null,
                    nodeCommitBlockContext: null,
                    atomicFlush: null,
                    flushCadence: null,
                    blockAccessListStore: bundle.BlockAccessLists,
                    withdrawalStore: bundle.Withdrawals);
                importSummary = await new RlpBlockFileImporter(importer)
                    .ImportAsync(options.ImportChainRlpPath, options.ImportBlocksDir);
            });

            LogStartup(app, options, config, importSummary);

            return app;
        }

        private static void LogStartup(
            WebApplication app, HarnessServerOptions options, DevChainServerConfig config, RlpBlockImportSummary importSummary)
        {
            var chain = config.GetLiveChainConfig();
            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Nethereum.Node.HarnessServer");

            logger.LogInformation(
                "Nethereum harness node ready - chainId={ChainId} fork={Fork} http=http://{HttpAddr}:{HttpPort} engine=http://{EngineAddr}:{EnginePort}",
                chain.ChainId, chain.PinnedFork, options.HttpAddr, options.HttpPort, options.AuthRpcAddr, options.AuthRpcPort);

            if (importSummary.AnyBlocksPresent)
                logger.LogInformation(
                    "Preloaded chain imported - imported={Imported} rejected={Rejected}",
                    importSummary.Imported, importSummary.Rejected);
        }
    }
}
