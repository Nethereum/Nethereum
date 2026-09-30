using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting
{
    public sealed class EmbeddedEnterpriseInfrastructureProvisioner : IEmbeddedInfrastructureProvisioner
    {
        public async Task<ProvisionedEnterpriseInfra> ProvisionAsync(Action<string>? log = null)
        {
            var bootstrap = await EnterpriseDemoHostBootstrap.StartAsync(log).ConfigureAwait(false);
            return bootstrap.Infra;
        }
    }
}
