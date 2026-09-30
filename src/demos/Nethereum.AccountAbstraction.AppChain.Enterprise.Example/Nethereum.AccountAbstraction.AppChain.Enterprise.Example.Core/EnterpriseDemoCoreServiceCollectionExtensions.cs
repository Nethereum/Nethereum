using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public static class EnterpriseDemoCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEnterpriseDemoCore(this IServiceCollection services)
        {
            services.AddSingleton<SessionState>();
            services.AddTransient<InfrastructureSetupViewModel>();
            services.AddTransient<EnterpriseAdminViewModel>();
            services.AddTransient<EnterpriseOperatorViewModel>();
            services.AddTransient<TieredApprovalViewModel>();
            services.AddTransient<OffboardViewModel>();
            return services;
        }
    }
}
