using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

/// <summary>
/// Default implementation of IPaymentRequirementsSelector that filters by network and scheme.
/// Spec Reference: Section 4.2 - Payment Requirements Selection
/// </summary>
public class DefaultPaymentRequirementsSelector : IPaymentRequirementsSelector
{
    /// <summary>
    /// Selects the first payment requirements that match the preferred network and scheme.
    /// </summary>
    public PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme)
    {
        ArgumentNullException.ThrowIfNull(availableRequirements, nameof(availableRequirements));
        ArgumentNullException.ThrowIfNull(preferredNetwork, nameof(preferredNetwork));
        ArgumentNullException.ThrowIfNull(preferredScheme, nameof(preferredScheme));

        var requirementsList = availableRequirements.ToList();

        if (!requirementsList.Any())
        {
            throw new InvalidOperationException("No payment requirements available");
        }

        // Find requirements matching preferred network and scheme
        var matching = requirementsList
            .FirstOrDefault(r =>
                r.Network?.Equals(preferredNetwork, StringComparison.OrdinalIgnoreCase) == true &&
                r.Scheme?.Equals(preferredScheme, StringComparison.OrdinalIgnoreCase) == true);

        if (matching == null)
        {
            throw new InvalidOperationException(
                $"No payment requirements found for network '{preferredNetwork}' and scheme '{preferredScheme}'. " +
                $"Available options: {string.Join(", ", requirementsList.Select(r => $"{r.Network}/{r.Scheme}"))}");
        }

        return matching;
    }
}
