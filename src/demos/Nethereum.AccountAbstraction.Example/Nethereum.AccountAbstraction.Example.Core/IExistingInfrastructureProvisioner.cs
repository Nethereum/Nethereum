using System;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public sealed record ExistingModuleAddresses(
        string EntryPointAddress,
        string NethereumAccountFactoryAddress,
        string EcdsaValidatorAddress,
        string WebAuthnValidatorAddress,
        string OwnableExecutorAddress,
        string SocialRecoveryAddress,
        string SmartSessionAddress,
        string SudoPolicyAddress,
        string UniActionPolicyAddress,
        string EcdsaSessionValidatorAddress,
        string AccountImplementationAddress);

    public sealed record ExistingInfrastructureRequest(
        string NodeRpcUrl,
        string BundlerUrl,
        string FunderPrivateKey,
        ExistingModuleAddresses Modules);

    public interface IExistingInfrastructureProvisioner
    {
        Task<ProvisionedInfra> ProvisionAsync(ExistingInfrastructureRequest request, Action<string>? log = null);
    }
}
