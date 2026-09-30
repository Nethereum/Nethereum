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
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevChain.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevChainServer(this IServiceCollection services, DevChainServerConfig config)
        {
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
                    bundle.BlockAccessLists);
            });

            services.AddSingleton<DevAccountManager>();

            services.AddSingleton<RpcHandlerRegistry>(provider => DevRpcHandlerExtensions.CreateDevChainRegistry());

            services.AddSingleton<RpcContext>(provider =>
            {
                var node = provider.GetRequiredService<DevChainNode>();
                return new RpcContext(node, config.ChainId, provider);
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
                services.AddEngineApiServer(new EngineApiServerConfig
                {
                    JwtSecretPath = config.EngineJwtSecretPath ?? Path.Combine(config.DataDir, EngineApiServerConfig.DefaultJwtSecretFileName),
                    Port = config.EnginePort,
                    BindAddress = config.EngineBindAddress ?? "127.0.0.1"
                });
            }

            return services;
        }

        public static IServiceCollection AddEngineApiServer(this IServiceCollection services, EngineApiServerConfig config)
        {
            services.AddSingleton(config);

            services.AddSingleton<IEngineApiService>(provider =>
            {
                var node = provider.GetRequiredService<DevChainNode>();
                var bundle = provider.GetRequiredService<IChainStoreBundle>();
                var importer = new BlockImporter(
                    node.BlockManager.Engine,
                    node.Blocks,
                    node.State,
                    node.Transactions,
                    node.Receipts,
                    node.Logs,
                    uncleStore: bundle.Uncles,
                    logger: null,
                    nodeCommitBlockContext: null,
                    atomicFlush: null,
                    flushCadence: null,
                    blockAccessListStore: bundle.BlockAccessLists,
                    withdrawalStore: bundle.Withdrawals);
                var forkChoice = new DifficultyForkChoice(node.Blocks);
                var payloadBuildRegistry = new PayloadBuildRegistry();

                return new EngineApiService(
                    node.Blocks, importer, node.BlockManager.BlockProducer, forkChoice, payloadBuildRegistry, node.Config);
            });

            services.AddSingleton(provider =>
                new EngineJwtValidator(EngineJwtSecret.LoadOrCreate(config.JwtSecretPath)));

            services.AddSingleton(provider =>
            {
                var registry = new RpcHandlerRegistry();
                registry.AddEngineHandlers();

                var devChainConfig = provider.GetRequiredService<DevChainServerConfig>();
                var rpcContext = provider.GetRequiredService<RpcContext>();
                var logger = devChainConfig.Verbose ? provider.GetRequiredService<ILogger<RpcDispatcher>>() : null;
                var dispatcher = new RpcDispatcher(registry, rpcContext, logger);
                var validator = provider.GetRequiredService<EngineJwtValidator>();

                return new EngineApiHost(registry, dispatcher, validator);
            });

            return services;
        }
    }
}
