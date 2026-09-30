using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Core;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed class EmbeddedInfrastructureProvisioner : IEmbeddedInfrastructureProvisioner
    {
        public async Task<ProvisionedInfra> ProvisionAsync(Action<string>? log = null)
        {
            var infra = await HostBootstrap.StartInfraAsync(log).ConfigureAwait(false);
            var stack = await HostBootstrap.DeployStackAsync(infra.OperatorWeb3, infra.Bundler, log).ConfigureAwait(false);
            return new ProvisionedInfra(stack, infra.OperatorWeb3, infra.Bundler, infra.Faucet, infra.Bootstrap);
        }
    }
}
