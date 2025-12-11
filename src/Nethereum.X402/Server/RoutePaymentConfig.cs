using Nethereum.X402.Models;

namespace Nethereum.X402.Server;

/// <summary>
/// Configuration for a route that requires payment.
/// Spec Reference: Section 8 - Server Implementation
/// </summary>
public class RoutePaymentConfig
{
    /// <summary>
    /// Path pattern for the route. Supports wildcards (*).
    /// Example: "/api/premium" or "/api/data/*"
    /// </summary>
    public string PathPattern { get; set; } = null!;

    /// <summary>
    /// HTTP method for the route (GET, POST, etc.).
    /// Null means any method.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Payment requirements for this route.
    /// </summary>
    public PaymentRequirements Requirements { get; set; } = null!;

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
}
