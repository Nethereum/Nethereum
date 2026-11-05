using Nethereum.Documentation;
using Nethereum.X402.Models;

namespace Nethereum.X402.Server;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "RoutePaymentConfig - a payment-protected route")]
public class RoutePaymentConfig
{
    public string PathPattern { get; set; } = null!;

    public string? Method { get; set; }

    public PaymentRequirements Requirements { get; set; } = null!;

    public List<PaymentRequirements>? Accepts { get; set; }

    public IReadOnlyList<PaymentRequirements> GetOffers() =>
        Accepts is { Count: > 0 } ? Accepts : new[] { Requirements };

    public ResourceInfo? Resource { get; set; }

    public Dictionary<string, object>? Extensions { get; set; }

    public RoutePaymentConfig()
    {
    }

    public RoutePaymentConfig(string pathPattern, PaymentRequirements requirements, string? method = null)
    {
        ArgumentNullException.ThrowIfNull(pathPattern, nameof(pathPattern));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        PathPattern = pathPattern;
        Requirements = requirements;
        Method = method;
    }

    public RoutePaymentConfig(string pathPattern, PaymentRequirements requirements, ResourceInfo resource, string? method = null)
        : this(pathPattern, requirements, method)
    {
        ArgumentNullException.ThrowIfNull(resource, nameof(resource));
        Resource = resource;
    }
}
