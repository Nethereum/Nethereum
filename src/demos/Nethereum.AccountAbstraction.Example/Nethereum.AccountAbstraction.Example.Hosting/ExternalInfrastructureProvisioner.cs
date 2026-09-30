using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Core;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed class ExternalInfrastructureProvisioner : IExternalInfrastructureProvisioner
    {
        public async Task<ProvisionedInfra> ProvisionAsync(ExternalInfrastructureRequest request, Action<string>? log = null)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            log ??= _ => { };

            var (funderWeb3, bundler) = await FunderInfrastructureConnection.ConnectAsync(
                request.NodeRpcUrl, request.FunderPrivateKey, request.BundlerUrl, log).ConfigureAwait(false);

            var stack = await HostBootstrap.DeployStackAsync(funderWeb3, bundler, log).ConfigureAwait(false);
            var faucet = new ExternalFaucet(funderWeb3);

            return new ProvisionedInfra(stack, funderWeb3, bundler, faucet, Resource: null);
        }
    }
}
