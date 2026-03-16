using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Rpc;
using Nethereum.DevChain.Rpc.Handlers;
using Nethereum.DevChain.Storage.Sqlite;

namespace Nethereum.DevChain.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevChainServer(this IServiceCollection services, DevChainServerConfig config)
        {
            services.AddSingleton(config);

            var storageMode = config.Storage?.ToLowerInvariant() ?? "sqlite";

            if (storageMode == "memory")
            {
                services.AddSingleton<IBlockStore>(new InMemoryBlockStore());
                services.AddSingleton<ITransactionStore>(provider =>
                    new InMemoryTransactionStore(provider.GetRequiredService<IBlockStore>()));
                services.AddSingleton<IReceiptStore>(new InMemoryReceiptStore());
                services.AddSingleton<ILogStore>(new InMemoryLogStore());
                services.AddSingleton<IStateStore>(new HistoricalStateStore(
                    new InMemoryStateStore(),
                    new InMemoryStateDiffStore(),
                    HistoricalStateOptions.DevChainDefault));
                services.AddSingleton<IFilterStore>(new InMemoryFilterStore());
                services.AddSingleton<ITrieNodeStore>(new InMemoryTrieNodeStore());

                services.AddSingleton(provider =>
                {
                    var devChainConfig = config.ToDevChainConfig();
                    return new DevChainNode(
                        devChainConfig,
                        provider.GetRequiredService<IBlockStore>(),
                        provider.GetRequiredService<ITransactionStore>(),
                        provider.GetRequiredService<IReceiptStore>(),
                        provider.GetRequiredService<ILogStore>(),
                        provider.GetRequiredService<IStateStore>(),
                        provider.GetRequiredService<IFilterStore>(),
                        provider.GetRequiredService<ITrieNodeStore>());
                });
            }
            else
            {
                var dbPath = config.Persist
                    ? Path.Combine(config.DataDir, "chain.db")
                    : null;

                var sqliteManager = new SqliteStorageManager(dbPath, deleteOnDispose: !config.Persist);
                services.AddSingleton(sqliteManager);

                services.AddSingleton<IBlockStore>(new SqliteBlockStore(sqliteManager));
                services.AddSingleton<ITransactionStore>(new SqliteTransactionStore(sqliteManager));
                services.AddSingleton<IReceiptStore>(new SqliteReceiptStore(sqliteManager));
                services.AddSingleton<ILogStore>(new SqliteLogStore(sqliteManager));
                services.AddSingleton<IStateStore>(new HistoricalStateStore(
                    new SqliteStateStore(sqliteManager),
                    new SqliteStateDiffStore(sqliteManager),
                    HistoricalStateOptions.DevChainDefault));
                services.AddSingleton<IFilterStore>(new InMemoryFilterStore());
                services.AddSingleton<ITrieNodeStore>(new SqliteTrieNodeStore(sqliteManager));

                services.AddSingleton(provider =>
                {
                    var devChainConfig = config.ToDevChainConfig();
                    return new DevChainNode(
                        devChainConfig,
                        provider.GetRequiredService<IBlockStore>(),
                        provider.GetRequiredService<ITransactionStore>(),
                        provider.GetRequiredService<IReceiptStore>(),
                        provider.GetRequiredService<ILogStore>(),
                        provider.GetRequiredService<IStateStore>(),
                        provider.GetRequiredService<IFilterStore>(),
                        provider.GetRequiredService<ITrieNodeStore>());
                });
            }

            services.AddSingleton<DevAccountManager>();

            services.AddSingleton<RpcHandlerRegistry>(provider =>
            {
                var registry = new RpcHandlerRegistry();

                registry.AddStandardHandlers();
                registry.AddDevHandlers();
                registry.AddAnvilAliases();

                registry.Override(new EthAccountsHandler());
                registry.Register(new HardhatImpersonateAccountHandler());
                registry.Register(new HardhatStopImpersonatingAccountHandler());

                return registry;
            });

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

            return services;
        }
    }
}
