using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;
using Npgsql;

namespace Nethereum.Mud.Repositories.Postgres
{
    public static class MudPostgresServiceCollectionExtensions
    {
        public static IServiceCollection AddMudPostgresProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);

            services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
                options.UseNpgsql(resolvedConnectionString)
                    .UseLowerCaseNamingConvention());

            ConfigureMudOptions(services, configuration);

            services.AddTransient<MudPostgresStoreRecordsProcessingService>();
            services.AddHostedService<MudPostgresProcessingHostedService>();

            return services;
        }

        public static IServiceCollection AddMudNormaliserProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);

            services.AddTransient<MudPostgresStoreRecordsTableRepository>();
            services.AddTransient(_ => new NpgsqlConnection(resolvedConnectionString));
            services.AddTransient<MudPostgresNormaliserProcessingService>();
            services.AddHostedService<MudPostgresNormaliserBackgroundService>();

            return services;
        }

        public static IServiceCollection AddMudWorldAddressDiscovery(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionString = null)
        {
            var resolvedConnectionString = ResolveConnectionString(configuration, connectionString);

            services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
                options.UseNpgsql(resolvedConnectionString)
                    .UseLowerCaseNamingConvention());

            services.AddTransient<MudPostgresStoreRecordsProcessingService>();
            services.AddTransient<MudPostgresStoreRecordsTableRepository>();
            services.AddTransient(_ => new NpgsqlConnection(resolvedConnectionString));
            services.AddTransient<MudPostgresNormaliserProcessingService>();

            var capturedConnectionString = resolvedConnectionString;
            services.AddHostedService(sp =>
                new MudWorldAddressDiscoveryService(
                    sp.GetRequiredService<ILogger<MudWorldAddressDiscoveryService>>(),
                    sp,
                    sp.GetRequiredService<IConfiguration>(),
                    capturedConnectionString));

            return services;
        }

        private static string ResolveConnectionString(IConfiguration configuration, string connectionString)
        {
            var resolved = connectionString
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("MudPostgresConnection");

            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:PostgresConnection configuration value.");
            }

            return resolved;
        }

        private static void ConfigureMudOptions(IServiceCollection services, IConfiguration configuration)
        {
            var optionsSection = configuration.GetSection("MudProcessing");
            services.AddOptions<MudPostgresProcessingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var source = optionsSection.Exists()
                        ? MudPostgresProcessingOptions.Load(optionsSection)
                        : MudPostgresProcessingOptions.Load(config);

                    options.Address = source.Address;
                    options.RpcUrl = source.RpcUrl;
                    options.StartAtBlockNumberIfNotProcessed = source.StartAtBlockNumberIfNotProcessed;
                    options.NumberOfBlocksToProcessPerRequest = source.NumberOfBlocksToProcessPerRequest;
                    options.RetryWeight = source.RetryWeight;
                    options.MinimumNumberOfConfirmations = source.MinimumNumberOfConfirmations;
                });
        }
    }
}
