namespace Nethereum.X402.Client;

/// <summary>
/// Configuration options for X402HttpClient automatic payment flow.
/// Spec Reference: Section 4 - Client Configuration
/// </summary>
public class X402HttpClientOptions
{
    public string MaxAmount { get; set; } = string.Empty;

    /// <summary>
    /// Preferred blockchain network for payments (e.g., "base-sepolia", "sepolia").
    /// </summary>
    public string PreferredNetwork { get; set; } = string.Empty;

    /// <summary>
    /// Preferred payment scheme (e.g., "exact").
    /// Default is "exact".
    /// </summary>
    public string PreferredScheme { get; set; } = "exact";

    /// <summary>
    /// Strategy for selecting payment requirements when multiple options are available.
    /// Default is DefaultPaymentRequirementsSelector.
    /// </summary>
    public IPaymentRequirementsSelector Selector { get; set; } = new DefaultPaymentRequirementsSelector();

    public PaymentPolicy Policy { get; set; } = new();

    /// <summary>
    /// Validates that all required options are set. The token, chain and EIP-712 domain are taken
    /// from the payment requirement returned by the server, not configured here.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PreferredNetwork))
            throw new InvalidOperationException("PreferredNetwork must be set");

        if (string.IsNullOrWhiteSpace(PreferredScheme))
            throw new InvalidOperationException("PreferredScheme must be set");

        if (string.IsNullOrWhiteSpace(MaxAmount) || !System.Numerics.BigInteger.TryParse(MaxAmount, out var max) || max <= 0)
            throw new InvalidOperationException("MaxAmount must be a positive integer in atomic units");

        ArgumentNullException.ThrowIfNull(Selector, nameof(Selector));
    }
}
