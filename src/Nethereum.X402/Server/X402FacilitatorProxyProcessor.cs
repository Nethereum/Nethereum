using Nethereum.Documentation;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nethereum.X402.Server;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402FacilitatorProxyProcessor - route matching and forwarding to a facilitator")]
public class X402FacilitatorProxyProcessor
{
    private readonly IFacilitatorClient _facilitator;
    private readonly List<(Regex Pattern, RoutePaymentConfig Config)> _compiledRoutes;

    public X402FacilitatorProxyProcessor(IFacilitatorClient facilitator, IEnumerable<RoutePaymentConfig> routes)
    {
        _facilitator = facilitator ?? throw new ArgumentNullException(nameof(facilitator));

        ArgumentNullException.ThrowIfNull(routes, nameof(routes));

        _compiledRoutes = routes
            .OrderBy(route => route.PathPattern, Comparer<string>.Create(X402RoutePattern.ComparePrecedence))
            .Select(route => (X402RoutePattern.Compile(route.PathPattern), route))
            .ToList();
    }

    public RoutePaymentConfig? FindMatchingRoute(string path, string? method = null)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));

        var candidates = X402RoutePattern.CandidatePaths(path);

        foreach (var (pattern, config) in _compiledRoutes)
        {
            if (!candidates.Any(candidate => pattern.IsMatch(candidate)))
                continue;

            if (config.Method != null &&
                !string.Equals(config.Method, method, StringComparison.OrdinalIgnoreCase))
                continue;

            return config;
        }

        return null;
    }

    public static PaymentRequirements? FindMatchingRequirements(RoutePaymentConfig route, PaymentPayload payment)
    {
        ArgumentNullException.ThrowIfNull(route, nameof(route));
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));

        return X402RequirementsMatcher.Find(route.GetOffers(), payment.Accepted);
    }

    public static PaymentPayload DecodePaymentHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header, nameof(header));

        try
        {
            var json = Encoding.UTF8.GetString(X402Base64.Decode(header));
            if (X402DuplicateJsonProperties.Contains(json))
                throw new ArgumentException("Duplicate property names in PAYMENT-SIGNATURE header", nameof(header));

            var payload = JsonSerializer.Deserialize<PaymentPayload>(json);

            if (payload == null)
                throw new InvalidOperationException("Decoded payment payload is null");

            return payload;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid base64 encoding in PAYMENT-SIGNATURE header", nameof(header), ex);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Malformed JSON in PAYMENT-SIGNATURE header", nameof(header), ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload payment,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        return await _facilitator.VerifyAsync(payment, requirements, cancellationToken);
    }

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload payment,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        return await _facilitator.SettleAsync(payment, requirements, cancellationToken);
    }

    public static string EncodeSettlementResponse(SettlementResponse response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));

        var json = JsonSerializer.Serialize(response);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
