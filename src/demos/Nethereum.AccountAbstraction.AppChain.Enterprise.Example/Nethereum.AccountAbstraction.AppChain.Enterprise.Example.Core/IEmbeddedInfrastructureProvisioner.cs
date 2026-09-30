using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.RPC;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public sealed record ProvisionedEnterpriseInfra(
        AppChainDeployment Deployment,
        IWeb3 Web3,
        IAAClient Client,
        AppChainAccountAdminService AdminService,
        IAccountAbstractionBundlerService Bundler,
        byte[] CapRuleId,
        string ValueCapCombinatorAddress,
        string PayableTargetAddress,
        string OwnableValidatorAddress,
        IAsyncDisposable? Resource = null);

    public interface IEmbeddedInfrastructureProvisioner
    {
        Task<ProvisionedEnterpriseInfra> ProvisionAsync(Action<string>? log = null);
    }
}
