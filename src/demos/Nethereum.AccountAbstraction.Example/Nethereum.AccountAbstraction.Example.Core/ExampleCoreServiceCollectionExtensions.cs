using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public static class ExampleCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddExampleCore(this IServiceCollection services)
        {
            services.AddSingleton<SessionState>();
            services.AddSingleton<AccountBannerViewModel>();
            services.AddSingleton<InfrastructureSetupViewModel>();
            services.AddTransient<SetupViewModel>();
            services.AddTransient<InteractionViewModel>();
            services.AddTransient<BatchViewModel>();
            services.AddTransient<GaslessViewModel>();
            services.AddTransient<CustomViewModel>();
            services.AddTransient<PasskeyAccountViewModel>();
            services.AddTransient<Eip7702AccountViewModel>();
            services.AddTransient<ModulesViewModel>();
            services.AddTransient<PoliciesViewModel>();
            services.AddTransient<WorkflowsViewModel>();
            services.AddTransient<SocialRecoveryViewModel>();
            services.AddTransient<DiagnosticsViewModel>();
            return services;
        }
    }
}
