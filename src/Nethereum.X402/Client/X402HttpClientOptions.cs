namespace Nethereum.X402.Client;

/// <summary>
/// Configuration options for X402HttpClient automatic payment flow.
/// Spec Reference: Section 4 - Client Configuration
/// </summary>
public class X402HttpClientOptions
{
    /// <summary>
    /// Maximum payment amount in USDC that the client will automatically approve.
    /// Default is 0.1 USDC.
    /// </summary>
    public decimal MaxPaymentAmount { get; set; } = 0.1m;

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

    /// <summary>
    /// Token name for EIP-712 signing (e.g., "USD Coin").
    /// </summary>
    public string TokenName { get; set; } = string.Empty;

    /// <summary>
    /// Token version for EIP-712 signing (e.g., "2").
    /// </summary>
    public string TokenVersion { get; set; } = string.Empty;

    /// <summary>
    /// Chain ID for the preferred network (e.g., 84532 for base-sepolia).
    /// </summary>
    public int ChainId { get; set; }

    /// <summary>
    /// Token contract address for the preferred network.
    /// </summary>
    public string TokenAddress { get; set; } = string.Empty;

    /// <summary>
    /// Validates that all required options are set.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PreferredNetwork))
            throw new InvalidOperationException("PreferredNetwork must be set");

        if (string.IsNullOrWhiteSpace(PreferredScheme))
            throw new InvalidOperationException("PreferredScheme must be set");

        if (string.IsNullOrWhiteSpace(TokenName))
            throw new InvalidOperationException("TokenName must be set");

        if (string.IsNullOrWhiteSpace(TokenVersion))
            throw new InvalidOperationException("TokenVersion must be set");

        if (ChainId <= 0)
            throw new InvalidOperationException("ChainId must be greater than 0");

        if (string.IsNullOrWhiteSpace(TokenAddress))
            throw new InvalidOperationException("TokenAddress must be set");

        if (MaxPaymentAmount <= 0)
            throw new InvalidOperationException("MaxPaymentAmount must be greater than 0");

        ArgumentNullException.ThrowIfNull(Selector, nameof(Selector));
    }
}
