using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AppChain.Anchoring.Messaging;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public static class MessageIndexServiceCollectionExtensions
    {
        public static IServiceCollection AddMessageIndexPostgresProcessing(
            this IServiceCollection services,
            IConfiguration configuration,
            string? connectionString = null)
        {
            var resolvedConnectionString = connectionString
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("MessageIndexPostgresConnection");

            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:PostgresConnection or ConnectionStrings:MessageIndexPostgresConnection configuration value.");
            }

            services.AddDbContext<MessageIndexDbContext>(options =>
                options.UseNpgsql(resolvedConnectionString)
                    .UseLowerCaseNamingConvention());

            var optionsSection = configuration.GetSection("MessageIndexProcessing");
            services.AddOptions<MessageIndexProcessingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    var source = optionsSection.Exists()
                        ? MessageIndexProcessingOptions.Load(optionsSection)
                        : MessageIndexProcessingOptions.Load(config);

                    options.RpcUrl = source.RpcUrl;
                    options.HubContractAddress = source.HubContractAddress;
                    options.TargetChainId = source.TargetChainId;
                    options.SourceChains = source.SourceChains;
                    options.StartAtBlockNumber = source.StartAtBlockNumber;
                    options.MinimumBlockConfirmations = source.MinimumBlockConfirmations;
                    options.ReorgBuffer = source.ReorgBuffer;
                    options.BlocksPerRequest = source.BlocksPerRequest;
                    options.RetryWeight = source.RetryWeight;
                });

            services.AddTransient<PostgresMessageIndexStore>();
            services.AddTransient<IMessageIndexStore>(sp => sp.GetRequiredService<PostgresMessageIndexStore>());
            services.AddHostedService<MessageIndexProcessingHostedService>();

            return services;
        }
    }
}
