using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Rpc.Handlers;

namespace Nethereum.DevChain.Server.Server
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevChainServer(this IServiceCollection services, DevChainServerConfig config)
        {
            services.AddSingleton(config);

            if (config.Storage?.ToLowerInvariant() == "rocksdb")
            {
                services.AddRocksDbStorage(new RocksDbStorageOptions
                {
                    DatabasePath = config.DataDir
                });

                services.AddSingleton(provider =>
                {
                    var devChainConfig = config.ToDevChainConfig();
                    var blockStore = provider.GetRequiredService<IBlockStore>();
                    var transactionStore = provider.GetRequiredService<ITransactionStore>();
                    var receiptStore = provider.GetRequiredService<IReceiptStore>();
                    var logStore = provider.GetRequiredService<ILogStore>();
                    var stateStore = provider.GetRequiredService<IStateStore>();
                    var filterStore = provider.GetRequiredService<IFilterStore>();
                    var trieNodeStore = provider.GetRequiredService<ITrieNodeStore>();

                    return new DevChainNode(
                        devChainConfig,
                        blockStore,
                        transactionStore,
                        receiptStore,
                        logStore,
                        stateStore,
                        filterStore,
                        trieNodeStore);
                });
            }
            else
            {
                services.AddSingleton(provider =>
                {
                    var devChainConfig = config.ToDevChainConfig();
                    return new DevChainNode(devChainConfig);
                });
            }

            services.AddSingleton<DevAccountManager>();

            services.AddSingleton<RpcHandlerRegistry>(provider =>
            {
                var registry = new RpcHandlerRegistry();

                registry.AddStandardHandlers();
                registry.AddDevHandlers();

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
