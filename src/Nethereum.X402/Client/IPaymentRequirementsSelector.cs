using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

/// <summary>
/// Interface for selecting payment requirements when multiple options are available.
/// Spec Reference: Section 4.2 - Payment Requirements Selection
/// </summary>
public interface IPaymentRequirementsSelector
{
    /// <summary>
    /// Selects appropriate payment requirements from available options.
    /// </summary>
    /// <param name="availableRequirements">List of payment requirements offered by the server</param>
    /// <param name="preferredNetwork">Preferred blockchain network (e.g., "base-sepolia", "sepolia")</param>
    /// <param name="preferredScheme">Preferred payment scheme (e.g., "exact")</param>
    /// <returns>Selected payment requirements</returns>
    /// <exception cref="InvalidOperationException">Thrown when no matching requirements are found</exception>
    PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme);
}
