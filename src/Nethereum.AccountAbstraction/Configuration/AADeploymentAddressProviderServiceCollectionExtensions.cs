using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nethereum.AccountAbstraction.Configuration
{
    public static class AADeploymentAddressProviderServiceCollectionExtensions
    {
        public static IServiceCollection AddAADeploymentAddressProvider(this IServiceCollection services)
        {
            services.TryAddSingleton<IAADeploymentAddressProvider, AADeploymentAddressProvider>();
            return services;
        }
    }
}
