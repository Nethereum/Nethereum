using System;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public sealed record ExternalInfrastructureRequest(string NodeRpcUrl, string BundlerUrl, string FunderPrivateKey);

    public interface IExternalInfrastructureProvisioner
    {
        Task<ProvisionedInfra> ProvisionAsync(ExternalInfrastructureRequest request, Action<string>? log = null);
    }
}
