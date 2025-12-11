using Nethereum.X402.Server;

namespace Nethereum.X402.AspNetCore;

/// <summary>
/// Configuration options for X402 middleware in ASP.NET Core.
/// Spec Reference: Section 8 - Server Implementation
/// </summary>
public class X402Options
{
    /// <summary>
    /// Route configurations for payment-protected endpoints.
    /// </summary>
    public List<RoutePaymentConfig> Routes { get; set; } = new();

    /// <summary>
    /// Base URL for the facilitator service.
    /// Example: "https://facilitator.x402.org"
    /// </summary>
    public string? FacilitatorUrl { get; set; }

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    public void Validate()
    {
        if (Routes == null || Routes.Count == 0)
        {
            throw new InvalidOperationException("At least one route must be configured for X402 middleware.");
        }

        if (string.IsNullOrWhiteSpace(FacilitatorUrl))
        {
            throw new InvalidOperationException("FacilitatorUrl must be configured for X402 middleware.");
        }

        foreach (var route in Routes)
        {
            if (string.IsNullOrWhiteSpace(route.PathPattern))
            {
                throw new InvalidOperationException("Route PathPattern cannot be null or empty.");
            }

            if (route.Requirements == null)
            {
                throw new InvalidOperationException($"Route '{route.PathPattern}' must have PaymentRequirements configured.");
            }

            ValidatePaymentRequirements(route.Requirements, route.PathPattern);
        }
    }

    private static void ValidatePaymentRequirements(Models.PaymentRequirements requirements, string routePath)
    {
        if (string.IsNullOrWhiteSpace(requirements.Scheme))
        {
            throw new InvalidOperationException($"Route '{routePath}': Scheme is required.");
        }

        if (string.IsNullOrWhiteSpace(requirements.Network))
        {
            throw new InvalidOperationException($"Route '{routePath}': Network is required.");
        }

        if (string.IsNullOrWhiteSpace(requirements.MaxAmountRequired))
        {
            throw new InvalidOperationException($"Route '{routePath}': MaxAmountRequired is required.");
        }

        if (string.IsNullOrWhiteSpace(requirements.PayTo))
        {
            throw new InvalidOperationException($"Route '{routePath}': PayTo address is required.");
        }

        if (string.IsNullOrWhiteSpace(requirements.Asset))
        {
            throw new InvalidOperationException($"Route '{routePath}': Asset is required.");
        }

        if (requirements.MaxTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"Route '{routePath}': MaxTimeoutSeconds must be positive.");
        }
    }
}
