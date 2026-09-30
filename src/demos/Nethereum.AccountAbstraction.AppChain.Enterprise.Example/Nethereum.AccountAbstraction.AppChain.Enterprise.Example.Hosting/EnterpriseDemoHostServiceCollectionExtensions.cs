using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting
{
    public static class EnterpriseDemoHostServiceCollectionExtensions
    {
        public static IServiceCollection AddEnterpriseDemoHostDeferred(this IServiceCollection services)
        {
            services.AddSingleton<IEmbeddedInfrastructureProvisioner, EmbeddedEnterpriseInfrastructureProvisioner>();
            services.AddSingleton<IExternalInfrastructureProvisioner, ExternalEnterpriseInfrastructureProvisioner>();
            services.AddEnterpriseDemoCore();
            return services;
        }
    }
}
