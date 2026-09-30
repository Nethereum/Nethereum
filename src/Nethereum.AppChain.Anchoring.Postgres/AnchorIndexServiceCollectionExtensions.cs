using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.AppChain.Anchoring.Postgres.Metrics;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public static class AnchorIndexServiceCollectionExtensions
    {
        public static IServiceCollection AddAnchorIndexPostgresStorage(
            this IServiceCollection services, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Missing connection string for Anchor Index storage.");

            EnsureDbContext(services, connectionString);

            services.AddScoped<IAnchorRecordRepository, PostgresAnchorRecordRepository>();
            services.AddScoped<IChainRegistrationRepository, PostgresChainRegistrationRepository>();
            services.AddScoped<IBlockProofRecordRepository, PostgresBlockProofRecordRepository>();
            services.AddScoped<IChainAnchoringSummaryRepository, PostgresChainAnchoringSummaryRepository>();

            return services;
        }

        public static IServiceCollection AddAnchorIndexProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null)
        {
            var resolved = ResolveConnectionString(configuration, connectionString);
            services.AddAnchorIndexPostgresStorage(resolved);

            var section = configuration.GetSection("AnchorIndexing");
            services.AddOptions<AnchorIndexProcessingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var bindSection = section.Exists() ? section : config;
                    bindSection.Bind(options);
                });

            services.AddTransient<AnchorIndexProgressRepository>();
            services.TryAddSingleton<AnchorIndexingMetrics>();
            services.AddTransient<AnchorIndexProcessingService>();
            services.AddHostedService<AnchorIndexProcessingHostedService>();

            return services;
        }

        public static IServiceCollection AddAnchorSummaryDenormalizerProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null)
        {
            var resolved = ResolveConnectionString(configuration, connectionString);
            EnsureDbContext(services, resolved);

            var section = configuration.GetSection("AnchorDenormalizer");
            services.AddOptions<AnchorSummaryDenormalizerOptions>()
                .Configure(options =>
                {
                    if (section.Exists()) section.Bind(options);
                });

            services.AddScoped<IChainAnchoringSummaryRepository, PostgresChainAnchoringSummaryRepository>();
            services.AddTransient<AnchorDenormalizerProgressRepository>();
            services.AddTransient<AnchorSummaryDenormalizerService>();
            services.AddHostedService<AnchorSummaryDenormalizerHostedService>();

            return services;
        }

        private static void EnsureDbContext(IServiceCollection services, string connectionString)
        {
            services.TryAddScoped(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<AnchorIndexDbContext>();
                optionsBuilder.UseNpgsql(connectionString).UseLowerCaseNamingConvention();
                return new AnchorIndexDbContext(optionsBuilder.Options);
            });
        }

        private static string ResolveConnectionString(IConfiguration configuration, string? connectionString)
        {
            var resolved = connectionString
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("AnchorIndexConnection");

            if (string.IsNullOrWhiteSpace(resolved))
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:PostgresConnection or ConnectionStrings:AnchorIndexConnection.");

            return resolved;
        }
    }
}
