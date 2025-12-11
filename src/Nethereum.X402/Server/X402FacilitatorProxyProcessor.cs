using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nethereum.X402.Server;

/// <summary>
/// Core payment processing logic for x402 servers using a remote facilitator.
/// Framework-agnostic - can be used with any .NET server framework.
/// Spec Reference: Section 8 - Server Implementation
///
/// This processor proxies payment verification and settlement to a remote facilitator service.
/// Use X402TransferWithAuthorisation3009Service or X402ReceiveWithAuthorisation3009Service
/// for direct blockchain interaction without a facilitator.
/// </summary>
public class X402FacilitatorProxyProcessor
{
    private readonly IFacilitatorClient _facilitator;
    private readonly List<(Regex Pattern, RoutePaymentConfig Config)> _compiledRoutes;

    public X402FacilitatorProxyProcessor(IFacilitatorClient facilitator, IEnumerable<RoutePaymentConfig> routes)
    {
        _facilitator = facilitator ?? throw new ArgumentNullException(nameof(facilitator));

        ArgumentNullException.ThrowIfNull(routes, nameof(routes));

        // Compile route patterns to regex for efficient matching
        _compiledRoutes = routes.Select(route =>
        {
            var pattern = ConvertPathPatternToRegex(route.PathPattern);
            return (new Regex(pattern, RegexOptions.IgnoreCase), route);
        }).ToList();

        // Sort by specificity (exact paths before wildcards)
        _compiledRoutes.Sort((a, b) =>
        {
            var aHasWildcard = a.Config.PathPattern.Contains('*');
            var bHasWildcard = b.Config.PathPattern.Contains('*');

            if (aHasWildcard && !bHasWildcard) return 1;
            if (!aHasWildcard && bHasWildcard) return -1;

            // Both same wildcard status, sort by length (more specific first)
            return b.Config.PathPattern.Length.CompareTo(a.Config.PathPattern.Length);
        });
    }

    /// <summary>
    /// Finds the payment requirements for a given path and HTTP method.
    /// Returns null if no matching route is found.
    /// Spec Reference: Section 8 - Server Implementation
    /// </summary>
    public PaymentRequirements? FindMatchingRoute(string path, string? method = null)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));

        foreach (var (pattern, config) in _compiledRoutes)
        {
            // Check if path matches
            if (!pattern.IsMatch(path))
                continue;

            // Check if method matches (null means any method)
            if (config.Method != null &&
                !string.Equals(config.Method, method, StringComparison.OrdinalIgnoreCase))
                continue;

            return config.Requirements;
        }

        return null;
    }

    /// <summary>
    /// Decodes a base64-encoded X-PAYMENT header into a PaymentPayload.
    /// Spec Reference: Section 5.2 - PaymentPayload Schema
    /// </summary>
    public static PaymentPayload DecodePaymentHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header, nameof(header));

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(header));
            var payload = JsonSerializer.Deserialize<PaymentPayload>(json);

            if (payload == null)
                throw new InvalidOperationException("Decoded payment payload is null");

            return payload;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid base64 encoding in X-PAYMENT header", nameof(header), ex);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Malformed JSON in X-PAYMENT header", nameof(header), ex);
        }
    }

    /// <summary>
    /// Verifies a payment payload against requirements using the facilitator.
    /// Spec Reference: Section 6.1.2 - Verification Steps, Section 7.1 - POST /verify
    /// </summary>
    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload payment,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        return await _facilitator.VerifyAsync(payment, requirements, cancellationToken);
    }

    /// <summary>
    /// Settles a verified payment using the facilitator.
    /// Spec Reference: Section 6.1.3 - Settlement, Section 7.2 - POST /settle
    /// </summary>
    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload payment,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        return await _facilitator.SettleAsync(payment, requirements, cancellationToken);
    }

    /// <summary>
    /// Encodes a settlement response for the X-PAYMENT-RESPONSE header.
    /// Spec Reference: Section 5.3 - SettlementResponse Schema
    /// </summary>
    public static string EncodeSettlementResponse(SettlementResponse response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));

        var json = JsonSerializer.Serialize(response);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Converts a path pattern with wildcards (*) to a regex pattern.
    /// Example: "/api/data/*" -> "^/api/data/.*$"
    /// </summary>
    private static string ConvertPathPatternToRegex(string pathPattern)
    {
        // Escape regex special characters except *
        var escaped = Regex.Escape(pathPattern);

        // Replace escaped \* with .* (match any characters)
        escaped = escaped.Replace("\\*", ".*");

        // Add anchors to match entire path
        return "^" + escaped + "$";
    }
}
