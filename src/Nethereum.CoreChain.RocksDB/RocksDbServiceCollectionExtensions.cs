using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public static class RocksDbServiceCollectionExtensions
    {
        public static IServiceCollection AddRocksDbStorage(this IServiceCollection services, string databasePath)
        {
            return services.AddRocksDbStorage(new RocksDbStorageOptions { DatabasePath = databasePath });
        }

        public static IServiceCollection AddRocksDbStorage(this IServiceCollection services, RocksDbStorageOptions options = null)
        {
            options ??= new RocksDbStorageOptions();

            services.AddSingleton(options);
            services.AddSingleton<RocksDbManager>(sp =>
            {
                var opts = sp.GetRequiredService<RocksDbStorageOptions>();
                return new RocksDbManager(opts);
            });

            services.AddSingleton<ITrieNodeStore, RocksDbTrieNodeStore>();
            services.AddSingleton<IBlockStore, RocksDbBlockStore>();
            services.AddSingleton<ITransactionStore>(sp =>
            {
                var manager = sp.GetRequiredService<RocksDbManager>();
                var blockStore = sp.GetRequiredService<IBlockStore>();
                return new RocksDbTransactionStore(manager, blockStore);
            });
            services.AddSingleton<IReceiptStore>(sp =>
            {
                var manager = sp.GetRequiredService<RocksDbManager>();
                var blockStore = sp.GetRequiredService<IBlockStore>();
                return new RocksDbReceiptStore(manager, blockStore);
            });
            services.AddSingleton<IStateStore, RocksDbStateStore>();
            services.AddSingleton<ILogStore, RocksDbLogStore>();
            services.AddSingleton<IFilterStore, RocksDbFilterStore>();

            return services;
        }
    }
}
