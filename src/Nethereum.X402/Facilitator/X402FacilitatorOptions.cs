using Nethereum.Documentation;
using Nethereum.X402.Blockchain;

namespace Nethereum.X402.Facilitator;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402FacilitatorOptions - settlement gas ceiling, asset allowlist, authorization policy and confirmations")]
public class X402FacilitatorOptions
{
    public const long DefaultMaxSettlementGas = 500_000;

    public const int DefaultMaxReceiptWaitSeconds = 3600;

    public long MaxSettlementGas { get; set; } = DefaultMaxSettlementGas;

    public Dictionary<string, List<string>> AllowedAssets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> FacilitatorAddresses { get; } = new();

    public const string DefaultRoutePrefix = "facilitator";

    public string RoutePrefix { get; set; } = DefaultRoutePrefix;

    public string? AuthorizationPolicy { get; set; }

    public int RequiredConfirmations { get; set; } = 1;

    public int MaxReceiptWaitSeconds { get; set; } = DefaultMaxReceiptWaitSeconds;

    public void Validate()
    {
        if (MaxSettlementGas <= 0)
            throw new InvalidOperationException("MaxSettlementGas must be positive");

        if (RequiredConfirmations < 1)
            throw new InvalidOperationException("RequiredConfirmations must be at least 1");

        if (RoutePrefix == null)
            throw new InvalidOperationException("RoutePrefix must not be null; use an empty string to serve the endpoints at the root");

        if (MaxReceiptWaitSeconds < 1)
            throw new InvalidOperationException("MaxReceiptWaitSeconds must be at least 1");

        foreach (var key in AllowedAssets.Keys)
        {
            if (!Caip2.TryParseEip155ChainId(key, out _))
                throw new ArgumentException(
                    $"AllowedAssets key '{key}' is not a canonical eip155 CAIP-2 network identifier (expected 'eip155:<chainId>').",
                    nameof(AllowedAssets));
        }
    }

    public bool IsAssetAllowed(string network, string? asset)
    {
        if (AllowedAssets.Count == 0 || string.IsNullOrEmpty(network))
            return true;

        var restricted = false;
        foreach (var entry in AllowedAssets)
        {
            if (!SameNetwork(entry.Key, network))
                continue;

            restricted = true;
            if (!string.IsNullOrEmpty(asset)
                && entry.Value.Any(candidate => Nethereum.Util.AddressExtensions.IsTheSameAddress(candidate, asset)))
                return true;
        }

        return !restricted;
    }

    private static bool SameNetwork(string configured, string network) =>
        Caip2.TryParseEip155ChainId(network, out var chainId)
            ? Caip2.TryParseEip155ChainId(configured, out var configuredChainId) && configuredChainId == chainId
            : string.Equals(configured, network, StringComparison.OrdinalIgnoreCase);
}
