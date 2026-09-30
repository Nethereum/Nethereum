using System;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Configuration;
using Nethereum.EVM;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class FullNodeHarness : IAsyncDisposable
    {
        public DevChainNode Node { get; }

        public BlockImporter RlpImporter { get; }

        private FullNodeHarness(DevChainNode node, BlockImporter importer)
        {
            Node = node;
            RlpImporter = importer;
        }

        public static Task<FullNodeHarness> ComposeAsync(
            StandardGenesisDocument document, HardforkName fork, HardforkRegistry registry) =>
            ComposeAsync(document, new ChainForkSchedule { Hardfork = fork.ToString() }, registry);

        public static async Task<FullNodeHarness> ComposeAsync(
            StandardGenesisDocument document, ChainForkSchedule schedule, HardforkRegistry registry)
        {
            var serverConfig = new DevChainServerConfig
            {
                Storage = "memory",
                EngineApiEnabled = false,
                AutoMine = false,
                Verbose = false,
            };

            var chainConfig = serverConfig.GetLiveChainConfig();
            StandardGenesisLoader.ApplyToChainConfig(chainConfig, document);
            chainConfig.Registry = registry;
            chainConfig.RewardPolicy = EthereumProofOfWorkRewardPolicy.Instance;
            chainConfig.BaseFee = document.BaseFeePerGas ?? 0;
            chainConfig.EstimateGasPaddingPercent = 0;

            schedule.ChainId = chainConfig.ForkSchedule.ChainId;
            chainConfig.ForkSchedule = schedule;

            var bundle = DevChainChainStoreBundle.OpenInMemory();

            var node = new DevChainNode(
                chainConfig,
                bundle.Blocks,
                bundle.Transactions,
                bundle.Receipts,
                bundle.Logs,
                bundle.State,
                new InMemoryFilterStore(),
                bundle.TrieNodes,
                blobStore: null,
                bundle.BlockAccessLists);

            await StandardGenesisLoader.PopulateAllocAsync(node.State, document.Alloc).ConfigureAwait(false);
            await node.StartAsync().ConfigureAwait(false);

            var importer = new BlockImporter(
                node.BlockManager.Engine, node.Blocks, node.State,
                node.Transactions, node.Receipts, node.Logs);

            return new FullNodeHarness(node, importer);
        }

        public EngineApiService BuildEngineApiService()
        {
            var forkChoice = new DifficultyForkChoice(Node.Blocks);
            var payloadBuildRegistry = new PayloadBuildRegistry();

            return new EngineApiService(
                Node.Blocks, RlpImporter, Node.BlockManager.BlockProducer,
                forkChoice, payloadBuildRegistry, Node.Config);
        }

        public RpcDispatcher BuildEngineRpcDispatcher()
        {
            var registry = new RpcHandlerRegistry().AddEngineHandlers();
            var services = new SingleServiceProvider(BuildEngineApiService());
            var context = new RpcContext(Node, Node.Config.ForkSchedule.ChainId, services);

            return new RpcDispatcher(registry, context);
        }

        public ValueTask DisposeAsync()
        {
            Node.Dispose();
            return default;
        }

        private sealed class SingleServiceProvider : IServiceProvider
        {
            private readonly IEngineApiService _engine;

            public SingleServiceProvider(IEngineApiService engine) => _engine = engine;

            public object GetService(Type serviceType) =>
                serviceType == typeof(IEngineApiService) ? _engine : null;
        }
    }
}
