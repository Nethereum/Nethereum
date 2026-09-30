using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting
{
    public sealed class ExternalEnterpriseInfrastructureProvisioner : IExternalInfrastructureProvisioner
    {
        public async Task<ProvisionedEnterpriseInfra> ProvisionAsync(ExternalInfrastructureRequest request, Action<string>? log = null)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            log ??= _ => { };

            var (funderWeb3, bundler, chainId) = await FunderInfrastructureConnection.ConnectAsync(
                request.NodeRpcUrl, request.FunderPrivateKey, request.BundlerUrl, log).ConfigureAwait(false);

            return await EnterpriseDemoHostBootstrap.DeployEnterpriseStackAsync(funderWeb3, bundler, chainId, log).ConfigureAwait(false);
        }
    }
}
