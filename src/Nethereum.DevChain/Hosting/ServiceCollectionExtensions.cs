using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Rpc;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.EngineApi.Hosting;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevChain.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevChainServer(this IServiceCollection services, DevChainServerConfig config)
        {
            ChainNodeEnodeValidator.RefuseMalformed(config.Node, "DevChain:Node");

            if (config.EngineApiEnabled)
            {
                config.AutoMine = false;
            }

            services.AddSingleton(config);

            var storageMode = config.Storage?.ToLowerInvariant() ?? "sqlite";

            if (storageMode == "memory")
            {
                services.AddSingleton<IChainStoreBundle>(DevChainChainStoreBundle.OpenInMemory());
            }
            else if (storageMode == "rocksdb")
            {
                var nodeConfig = new ChainNodeConfig();
                nodeConfig.Storage.DataDirectory = config.DataDir;
                nodeConfig.Storage.PathKeyedState = true;

                var storage = ChainNodeStorage.Open(nodeConfig);
                services.AddSingleton(storage);
                services.AddSingleton<IChainStoreBundle>(storage.Bundle);
            }
            else
            {
                var dbPath = config.Persist
                    ? Path.Combine(config.DataDir, "chain.db")
                    : null;

                var sqliteManager = new SqliteStorageManager(dbPath, deleteOnDispose: !config.Persist);
                services.AddSingleton(sqliteManager);
                services.AddSingleton<IChainStoreBundle>(DevChainChainStoreBundle.OpenSqlite(sqliteManager));
            }

            services.AddSingleton<IBlockStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().Blocks);
            services.AddSingleton<ITransactionStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().Transactions);
            services.AddSingleton<IReceiptStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().Receipts);
            services.AddSingleton<ILogStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().Logs);
            services.AddSingleton<IStateStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().State);
            services.AddSingleton<ITrieNodeStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().TrieNodes);
            services.AddSingleton<IBlockAccessListStore>(provider =>
                provider.GetRequiredService<IChainStoreBundle>().BlockAccessLists);
            services.AddSingleton<IFilterStore>(new InMemoryFilterStore());

            services.AddSingleton(provider =>
            {
                var bundle = provider.GetRequiredService<IChainStoreBundle>();
                return new DevChainNode(
                    config.GetLiveChainConfig(),
                    bundle.Blocks,
                    bundle.Transactions,
                    bundle.Receipts,
                    bundle.Logs,
                    bundle.State,
                    provider.GetRequiredService<IFilterStore>(),
                    bundle.TrieNodes,
                    blobStore: null,
                    bundle.BlockAccessLists,
                    bundle.Uncles);
            });

            services.AddSingleton<DevAccountManager>();

            services.AddSingleton<RpcHandlerRegistry>(provider => DevRpcHandlerExtensions.CreateDevChainRegistry());

            services.AddSingleton<RpcContext>(provider =>
            {
                var node = provider.GetRequiredService<DevChainNode>();
                return new RpcContext(node, config.Chain.ChainId, provider);
            });

            services.AddSingleton<RpcDispatcher>(provider =>
            {
                var registry = provider.GetRequiredService<RpcHandlerRegistry>();
                var context = provider.GetRequiredService<RpcContext>();
                var logger = config.Verbose ? provider.GetRequiredService<ILogger<RpcDispatcher>>() : null;

                return new RpcDispatcher(registry, context, logger);
            });

            if (config.EngineApiEnabled)
            {
                services.AddEngineApiServer(
                    new EngineApiServerConfig
                    {
                        JwtSecretPath = config.EngineJwtSecretPath ?? Path.Combine(config.DataDir, EngineApiServerConfig.DefaultJwtSecretFileName),
                        Port = config.EnginePort,
                        BindAddress = config.EngineBindAddress ?? "127.0.0.1"
                    },
                    CreateEngineService,
                    config.Verbose);
            }

            return services;
        }

        public static BlockImporter CreateEngineImporter(
            DevChainNode node, IChainStoreBundle bundle, IFlushCadence flushCadence = null) =>
            ExecutionStackFactory.CreateImporter(
                bundle, node.BlockManager.Engine,
                new ExecutionStackOptions { SideChain = new SideChainOptions(), FlushCadence = flushCadence });

        private static IEngineApiService CreateEngineService(IServiceProvider provider)
        {
            var node = provider.GetRequiredService<DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var importer = CreateEngineImporter(
                node, bundle,
                FlushCadences.ForBlocks(provider.GetRequiredService<DevChainServerConfig>().Node.Storage.FlushCadenceBlocks));

            return new EngineApiService(
                node.Blocks, importer, node.BlockManager.BlockProducer, forkChoice: null, new PayloadBuildRegistry(), node.Config);
        }
    }
}
