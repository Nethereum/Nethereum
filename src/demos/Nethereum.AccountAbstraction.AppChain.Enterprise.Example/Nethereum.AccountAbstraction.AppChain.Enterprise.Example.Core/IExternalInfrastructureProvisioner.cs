namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public sealed record ExternalInfrastructureRequest(string NodeRpcUrl, string BundlerUrl, string FunderPrivateKey);

    public interface IExternalInfrastructureProvisioner
    {
        Task<ProvisionedEnterpriseInfra> ProvisionAsync(ExternalInfrastructureRequest request, Action<string>? log = null);
    }
}
