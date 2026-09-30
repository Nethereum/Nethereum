using Microsoft.Extensions.DependencyInjection;
using Nethereum.AppChain.Anchoring.Postgres;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;
using Nethereum.Explorer.Anchoring.Services;
using Nethereum.Explorer.Services;

namespace Nethereum.Explorer.Anchoring
{
    public static class AnchorExplorerServiceCollectionExtensions
    {
        public static IServiceCollection AddAnchorExplorerServices(
            this IServiceCollection services, string? connectionString = null)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddAnchorIndexPostgresStorage(connectionString);
                services.AddScoped<IAnchorExplorerService>(sp =>
                {
                    var anchorRepo = sp.GetService<IAnchorRecordRepository>();
                    var chainRepo = sp.GetService<IChainRegistrationRepository>();
                    var proofRepo = sp.GetService<IBlockProofRecordRepository>();
                    var summaryRepo = sp.GetService<IChainAnchoringSummaryRepository>();

                    if (anchorRepo != null && chainRepo != null && proofRepo != null && summaryRepo != null)
                        return new AnchorExplorerService(anchorRepo, chainRepo, proofRepo, summaryRepo);

                    return new NullAnchorExplorerService();
                });
            }
            else
            {
                services.AddScoped<IAnchorExplorerService, NullAnchorExplorerService>();
            }

            services.AddScoped<IExplorerNavContributor, AnchorExplorerNavContributor>();
            services.AddSingleton<AnchoringLocalizer>();

            return services;
        }
    }
}
