using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

public class DefaultPaymentRequirementsSelector : IPaymentRequirementsSelector
{
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

        var candidates = requirementsList
            .Where(X402RequirementSupport.IsSupportedByThisLibrary)
            .Where(r =>
                r.Network?.Equals(preferredNetwork, StringComparison.OrdinalIgnoreCase) == true &&
                r.Scheme?.Equals(preferredScheme, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var matching = candidates.FirstOrDefault(X402RequirementSupport.IsAuthorizationFlow)
            ?? candidates.FirstOrDefault();

        if (matching == null)
        {
            throw new InvalidOperationException(
                $"No payment requirements found for network '{preferredNetwork}' and scheme '{preferredScheme}'. " +
                $"Available options: {string.Join(", ", requirementsList.Select(r => $"{r.Network}/{r.Scheme}/{X402RequirementSupport.ResolveAssetTransferMethod(r)}"))}");
        }

        return matching;
    }
}
