using System;
using System.Threading.Tasks;
using Nethereum.RPC;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public sealed record ProvisionedInfra(
        DeployedStack Stack,
        IWeb3 Web3,
        IAccountAbstractionBundlerService Bundler,
        IDevChainFaucet Faucet,
        IAsyncDisposable? Resource = null);

    public interface IEmbeddedInfrastructureProvisioner
    {
        Task<ProvisionedInfra> ProvisionAsync(Action<string>? log = null);
    }
}
