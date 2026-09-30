using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Example.Core;
using Nethereum.RPC;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public static class ExampleHostServiceCollectionExtensions
    {
        private const string RelyingPartyId = "localhost";

        public static IServiceCollection AddExampleHost(this IServiceCollection services, HostBootstrap infra)
        {
            services.AddSingleton(infra);
            services.AddSingleton<IWeb3>(infra.Bootstrap.OperatorWeb3);
            services.AddSingleton(infra.Bundler);
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(infra.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(infra.DeploymentAddresses)
                .UseBundler(infra.Bundler));
            services.AddSingleton(infra.TestCounter);
            services.AddSingleton(infra.BookingRegistry);
            services.AddSingleton(infra.PaymasterConfig);
            services.AddSingleton(new WebAuthnAccountConfig(infra.WebAuthnValidatorAddress, RelyingPartyId));
            services.AddSingleton(infra.OwnableExecutor);
            services.AddSingleton(new SocialRecoveryAccountConfig(infra.SocialRecoveryAddress, infra.DeploymentAddresses.EcdsaValidatorAddress));
            services.AddSingleton(new Eip7702AccountConfig(infra.NethereumAccountImplementationAddress, infra.DeploymentAddresses.EcdsaValidatorAddress));
            services.AddSingleton(infra.SmartSession);
            services.AddSingleton(infra.PoliciesConfig);
            services.AddSingleton(infra.Faucet);
            services.AddSingleton<IEmbeddedInfrastructureProvisioner, EmbeddedInfrastructureProvisioner>();
            services.AddSingleton<IExternalInfrastructureProvisioner, ExternalInfrastructureProvisioner>();
            services.AddSingleton<IExistingInfrastructureProvisioner, ExistingInfrastructureProvisioner>();
            services.AddExampleCore();

            services.Replace(ServiceDescriptor.Singleton(_ =>
            {
                var session = new SessionState();
                session.PublishInfra(infra.Stack, infra.Bootstrap.OperatorWeb3, infra.Bundler, infra.Faucet);
                return session;
            }));
            return services;
        }

        public static IServiceCollection AddExampleHostDeferred(this IServiceCollection services)
        {
            services.AddSingleton<IEmbeddedInfrastructureProvisioner, EmbeddedInfrastructureProvisioner>();
            services.AddSingleton<IExternalInfrastructureProvisioner, ExternalInfrastructureProvisioner>();
            services.AddSingleton<IExistingInfrastructureProvisioner, ExistingInfrastructureProvisioner>();
            services.AddExampleCore();
            return services;
        }
    }
}
