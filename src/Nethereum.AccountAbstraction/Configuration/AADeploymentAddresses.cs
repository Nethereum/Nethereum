using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Configuration
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "AADeploymentAddresses - the per-chain AA contract addresses the client needs")]
    public record AADeploymentAddresses(
        string EntryPointAddress,
        string NethereumAccountFactoryAddress,
        string EcdsaValidatorAddress,
        string VerifyingPaymasterAddress);
}
