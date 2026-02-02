using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB
{
    public static class BundlerRocksDbServiceCollectionExtensions
    {
        public static IServiceCollection AddBundlerRocksDbStorage(
            this IServiceCollection services,
            string databasePath)
        {
            return services.AddBundlerRocksDbStorage(new BundlerRocksDbOptions { DatabasePath = databasePath });
        }

        public static IServiceCollection AddBundlerRocksDbStorage(
            this IServiceCollection services,
            BundlerRocksDbOptions? options = null,
            ReputationConfig? reputationConfig = null)
        {
            options ??= new BundlerRocksDbOptions();
            reputationConfig ??= new ReputationConfig();

            services.AddSingleton(options);
            services.AddSingleton(reputationConfig);

            services.AddSingleton<BundlerRocksDbManager>(sp =>
            {
                var opts = sp.GetRequiredService<BundlerRocksDbOptions>();
                return new BundlerRocksDbManager(opts);
            });

            services.AddSingleton<IUserOpMempool>(sp =>
            {
                var manager = sp.GetRequiredService<BundlerRocksDbManager>();
                var opts = sp.GetRequiredService<BundlerRocksDbOptions>();
                return new RocksDbUserOpMempool(manager, opts);
            });

            services.AddSingleton<IReputationStore>(sp =>
            {
                var manager = sp.GetRequiredService<BundlerRocksDbManager>();
                var config = sp.GetRequiredService<ReputationConfig>();
                return new RocksDbReputationStore(manager, config);
            });

            return services;
        }

        public static IServiceCollection AddBundlerRocksDbMempoolOnly(
            this IServiceCollection services,
            BundlerRocksDbOptions? options = null)
        {
            options ??= new BundlerRocksDbOptions();

            services.AddSingleton(options);

            services.AddSingleton<BundlerRocksDbManager>(sp =>
            {
                var opts = sp.GetRequiredService<BundlerRocksDbOptions>();
                return new BundlerRocksDbManager(opts);
            });

            services.AddSingleton<IUserOpMempool>(sp =>
            {
                var manager = sp.GetRequiredService<BundlerRocksDbManager>();
                var opts = sp.GetRequiredService<BundlerRocksDbOptions>();
                return new RocksDbUserOpMempool(manager, opts);
            });

            return services;
        }

        public static IServiceCollection AddBundlerRocksDbReputationOnly(
            this IServiceCollection services,
            BundlerRocksDbOptions? options = null,
            ReputationConfig? reputationConfig = null)
        {
            options ??= new BundlerRocksDbOptions();
            reputationConfig ??= new ReputationConfig();

            services.AddSingleton(options);
            services.AddSingleton(reputationConfig);

            if (!services.Any(s => s.ServiceType == typeof(BundlerRocksDbManager)))
            {
                services.AddSingleton<BundlerRocksDbManager>(sp =>
                {
                    var opts = sp.GetRequiredService<BundlerRocksDbOptions>();
                    return new BundlerRocksDbManager(opts);
                });
            }

            services.AddSingleton<IReputationStore>(sp =>
            {
                var manager = sp.GetRequiredService<BundlerRocksDbManager>();
                var config = sp.GetRequiredService<ReputationConfig>();
                return new RocksDbReputationStore(manager, config);
            });

            return services;
        }
    }
}
