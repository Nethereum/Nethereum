using Nethereum.JsonRpc.Client;

namespace Nethereum.X402.Client;

public class X402HttpClientOptions
{
    public string MaxAmount { get; set; } = string.Empty;

    public string PreferredNetwork { get; set; } = string.Empty;

    public string PreferredScheme { get; set; } = "exact";

    public IPaymentRequirementsSelector Selector { get; set; } = new DefaultPaymentRequirementsSelector();

    public PaymentPolicy Policy { get; set; } = new();

    public Dictionary<int, IClient>? RpcClientsByChainId { get; set; }

    public bool CapSponsoredApprovalToPaymentAmount { get; set; }

    public const int DefaultMaxAuthorizationValiditySeconds = 3600;

    public int MaxAuthorizationValiditySeconds { get; set; } = DefaultMaxAuthorizationValiditySeconds;

    public const int DefaultMaxResponseBytes = 1024 * 1024;

    public int MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PreferredNetwork))
            throw new InvalidOperationException("PreferredNetwork must be set");

        if (string.IsNullOrWhiteSpace(PreferredScheme))
            throw new InvalidOperationException("PreferredScheme must be set");

        if (string.IsNullOrWhiteSpace(MaxAmount) || !System.Numerics.BigInteger.TryParse(MaxAmount, out var max) || max <= 0)
            throw new InvalidOperationException("MaxAmount must be a positive integer in atomic units");

        if (MaxAuthorizationValiditySeconds <= 0)
            throw new InvalidOperationException("MaxAuthorizationValiditySeconds must be positive");

        if (MaxResponseBytes <= 0)
            throw new InvalidOperationException("MaxResponseBytes must be positive");

        ArgumentNullException.ThrowIfNull(Selector, nameof(Selector));
    }
}
