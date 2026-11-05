using Nethereum.Documentation;
using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "IPaymentRequirementsSelector - picks the requirement the client pays")]
public interface IPaymentRequirementsSelector
{
    PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme);
}
