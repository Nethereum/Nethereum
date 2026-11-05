using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles.Bls;
using Newtonsoft.Json.Linq;

namespace Nethereum.Node.HarnessServer
{
    public static class HarnessNodeHost
    {
        public static readonly HardforkRegistry Registry = Bls12381AwareMainnetHardforkRegistry.Build(
            Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
            new Nethereum.Signer.Bls.Herumi.Bls12381Operations());

        public static async Task<WebApplication> BuildAsync(HarnessServerOptions options, string[] args)
        {
            var document = LoadGenesis(options.GenesisPath, options.HiveEnvironment);

            var builder = WebApplication.CreateBuilder(Nethereum.CoreChain.Hosting.HostArguments.WithoutConfigReload(args));

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

            ConfigureChain(config, document, scheduleOverride: null, HiveGenesisMapping.TargetGasLimit(options.HiveEnvironment));

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
                await StartNodeAsync(node, document);

                var importer = CreateImporter(node, app.Services.GetRequiredService<IChainStoreBundle>());
                importSummary = await new RlpBlockFileImporter(importer)
                    .ImportAsync(options.ImportChainRlpPath, options.ImportBlocksDir);
            });

            LogStartup(app, options, config, importSummary);

            return app;
        }

        public static StandardGenesisDocument LoadGenesis(string path, IReadOnlyDictionary<string, string> hiveEnvironment)
        {
            var root = JObject.Parse(File.ReadAllText(path));
            return StandardGenesisLoader.Parse(
                HiveGenesisMapping.IsHiveEnvironment(hiveEnvironment) ? HiveGenesisMapping.Apply(root, hiveEnvironment) : root);
        }

        public static void ConfigureChain(
            DevChainServerConfig config, StandardGenesisDocument document, ChainForkSchedule? scheduleOverride,
            long targetGasLimit = HiveGenesisMapping.DefaultTargetGasLimit)
        {
            var chain = config.GetLiveChainConfig();
            StandardGenesisLoader.ApplyToChainConfig(chain, document);
            chain.Registry = Registry;
            chain.RewardPolicy = EthereumProofOfWorkRewardPolicy.Instance;
            chain.PredeploySystemContracts = !StandardGenesisLoader.DeclaresAnySystemContract(document);
            chain.BaseFee = document.BaseFeePerGas ?? 0;
            chain.EstimateGasPaddingPercent = 0;
            chain.TargetGasLimit = targetGasLimit;

            if (scheduleOverride != null)
            {
                scheduleOverride.ChainId = chain.ForkSchedule.ChainId;
                chain.ForkSchedule = scheduleOverride;
            }
        }

        public static async Task StartNodeAsync(DevChainNode node, StandardGenesisDocument document)
        {
            await StandardGenesisLoader.PopulateAllocAsync(node.State, document.Alloc);
            await node.StartAsync();
        }

        public static BlockImporter CreateImporter(DevChainNode node, IChainStoreBundle bundle) =>
            ExecutionStackFactory.CreateImporter(
                bundle, node.BlockManager.Engine,
                new ExecutionStackOptions { SideChain = new SideChainOptions { AdoptByDifficulty = true } });

        public static async Task<HarnessNode> ComposeInProcessAsync(
            StandardGenesisDocument document, ChainForkSchedule? scheduleOverride = null,
            long targetGasLimit = HiveGenesisMapping.DefaultTargetGasLimit)
        {
            var config = new DevChainServerConfig
            {
                Storage = "memory",
                Persist = true,
                EngineApiEnabled = true,
            };
            ConfigureChain(config, document, scheduleOverride, targetGasLimit);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDevChainServer(config);
            var provider = services.BuildServiceProvider();

            var node = provider.GetRequiredService<DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var composed = await DevChainComposition.ComposeAsync(
                config, node, bundle, n => StartNodeAsync(n, document),
                provider.GetRequiredService<ILoggerFactory>());

            return new HarnessNode(provider, composed, CreateImporter(node, bundle));
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

            foreach (var rejection in importSummary.Rejections)
                logger.LogWarning("Preloaded block rejected - {Rejection}", rejection);
        }
    }
}
