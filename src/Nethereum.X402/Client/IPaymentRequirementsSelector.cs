using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

/// <summary>
/// Interface for selecting payment requirements when multiple options are available.
/// Spec Reference: Section 4.2 - Payment Requirements Selection
/// </summary>
public interface IPaymentRequirementsSelector
{
    PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme);
}
