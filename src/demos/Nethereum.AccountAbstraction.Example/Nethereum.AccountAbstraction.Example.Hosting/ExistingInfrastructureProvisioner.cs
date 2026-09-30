using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Core;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed class ExistingInfrastructureProvisioner : IExistingInfrastructureProvisioner
    {
        public async Task<ProvisionedInfra> ProvisionAsync(ExistingInfrastructureRequest request, Action<string>? log = null)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            log ??= _ => { };

            var (funderWeb3, bundler) = await FunderInfrastructureConnection.ConnectAsync(
                request.NodeRpcUrl, request.FunderPrivateKey, request.BundlerUrl, log).ConfigureAwait(false);

            var stack = await HostBootstrap.BuildFromExistingAsync(funderWeb3, bundler, request.Modules, log).ConfigureAwait(false);
            var faucet = new ExternalFaucet(funderWeb3);

            return new ProvisionedInfra(stack, funderWeb3, bundler, faucet, Resource: null);
        }
    }
}
