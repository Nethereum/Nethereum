using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainStorage.Token.Postgres.Repositories;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public static class TokenPostgresServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenPostgresRepositories(
            this IServiceCollection services,
            string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing connection string for Token Postgres repositories.");
            }

            EnsureTokenDbContext(services, connectionString);

            services.AddTransient<ITokenTransferLogRepository, TokenPostgresTransferLogRepository>();
            services.AddTransient<INonCanonicalTokenTransferLogRepository, TokenPostgresTransferLogRepository>();
            services.AddTransient<ITokenBalanceRepository, TokenPostgresBalanceRepository>();
            services.AddTransient<INFTInventoryRepository, TokenPostgresNFTInventoryRepository>();
            services.AddTransient<ITokenMetadataRepository, TokenPostgresMetadataRepository>();

            return services;
        }

        public static IServiceCollection AddTokenLogPostgresProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);
            EnsureTokenDbContext(services, resolvedConnectionString);

            var sectionName = "TokenLogProcessing";
            var section = configuration.GetSection(sectionName);
            services.AddOptions<TokenLogProcessingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var bindSection = section.Exists() ? section : config;
                    bindSection.Bind(options);
                });

            services.AddTransient<TokenLogPostgresProcessingService>();
            services.AddHostedService<TokenLogPostgresHostedService>();

            return services;
        }

        public static IServiceCollection AddTokenDenormalizerProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);
            EnsureTokenDbContext(services, resolvedConnectionString);

            var section = configuration.GetSection("TokenDenormalizer");
            services.AddOptions<TokenDenormalizerOptions>()
                .Configure(options =>
                {
                    if (section.Exists())
                        section.Bind(options);
                });

            services.AddTransient<ITokenTransferLogRepository, TokenPostgresTransferLogRepository>();
            services.AddTransient<INonCanonicalTokenTransferLogRepository, TokenPostgresTransferLogRepository>();
            services.AddTransient<DenormalizerProgressRepository>();

            services.AddTransient<TokenDenormalizerService>();
            services.AddHostedService<TokenDenormalizerHostedService>();

            return services;
        }

        public static IServiceCollection AddTokenBalanceAggregationProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);
            EnsureTokenDbContext(services, resolvedConnectionString);

            var section = configuration.GetSection("TokenBalanceAggregation");
            services.AddOptions<TokenBalanceAggregationOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var bindSection = section.Exists() ? section : config;
                    bindSection.Bind(options);
                });

            services.AddTransient<ITokenBalanceRepository, TokenPostgresBalanceRepository>();
            services.AddTransient<INFTInventoryRepository, TokenPostgresNFTInventoryRepository>();
            services.AddTransient<BalanceAggregationProgressRepository>();

            services.AddTransient<TokenBalanceRpcAggregationService>();
            services.AddHostedService<TokenBalanceAggregationHostedService>();

            return services;
        }

        private static void EnsureTokenDbContext(IServiceCollection services, string connectionString)
        {
            services.TryAddScoped(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TokenPostgresDbContext>();
                optionsBuilder.UseNpgsql(connectionString).UseLowerCaseNamingConvention();
                return new TokenPostgresDbContext(optionsBuilder.Options);
            });
        }

        private static string ResolveConnectionString(IConfiguration configuration, string connectionString)
        {
            var resolved = connectionString
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("TokenPostgresConnection");

            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:PostgresConnection or ConnectionStrings:TokenPostgresConnection configuration value.");
            }

            return resolved;
        }
    }
}
