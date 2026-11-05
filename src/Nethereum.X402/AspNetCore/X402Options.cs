using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.AspNetCore;

public class X402Options
{
    public List<RoutePaymentConfig> Routes { get; set; } = new();

    public string? FacilitatorUrl { get; set; }

    public long MaxBufferedResponseBytes { get; set; } = 1024 * 1024;

    public bool EnablePaymentReplayProtection { get; set; } = true;

    public TimeSpan PaymentReplayRetention { get; set; } = TimeSpan.FromHours(1);

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

        if (EnablePaymentReplayProtection && PaymentReplayRetention <= TimeSpan.Zero)
        {
            throw new ArgumentException("PaymentReplayRetention must be greater than zero when replay protection is enabled.", nameof(PaymentReplayRetention));
        }

        foreach (var route in Routes)
        {
            if (string.IsNullOrWhiteSpace(route.PathPattern))
            {
                throw new InvalidOperationException("Route PathPattern cannot be null or empty.");
            }

            if ((route.PathPattern != "*" && route.PathPattern[0] != '/') || route.PathPattern.IndexOfAny(new[] { '?', '#' }) >= 0 || route.PathPattern.Contains("://"))
            {
                throw new ArgumentException(
                    $"Route PathPattern '{route.PathPattern}' must be '*' or start with '/' and contain no '?', '#' or '://'.", nameof(Routes));
            }

            if (route.Requirements == null && route.Accepts is not { Count: > 0 })
            {
                throw new InvalidOperationException($"Route '{route.PathPattern}' must have PaymentRequirements configured.");
            }

            foreach (var offer in route.GetOffers())
            {
                if (offer == null)
                {
                    throw new InvalidOperationException($"Route '{route.PathPattern}' has a null entry in Accepts.");
                }

                ValidatePaymentRequirements(offer, route.PathPattern);
            }

            ValidateResource(route.Resource, route.PathPattern);
        }
    }

    private const int MaxServiceNameLength = 32;
    private const int MaxTags = 5;
    private const int MaxTagLength = 32;
    private const int MaxIconUrlLength = 2048;

    private static void ValidateResource(ResourceInfo? resource, string routePath)
    {
        if (resource == null)
            return;

        if (resource.ServiceName != null && !IsPrintableAscii(resource.ServiceName, MaxServiceNameLength))
        {
            throw new InvalidOperationException(
                $"Route '{routePath}': Resource ServiceName must be printable ASCII of at most {MaxServiceNameLength} characters.");
        }

        if (resource.Tags != null && (resource.Tags.Count > MaxTags || !resource.Tags.All(tag => IsPrintableAscii(tag, MaxTagLength))))
        {
            throw new InvalidOperationException(
                $"Route '{routePath}': Resource Tags must be at most {MaxTags} entries, each printable ASCII of at most {MaxTagLength} characters.");
        }

        if (resource.IconUrl != null && !IsAbsoluteHttpUrl(resource.IconUrl, MaxIconUrlLength))
        {
            throw new InvalidOperationException(
                $"Route '{routePath}': Resource IconUrl must be an absolute http or https URL of at most {MaxIconUrlLength} characters.");
        }
    }

    private static bool IsPrintableAscii(string value, int maxLength) =>
        value != null && value.Length <= maxLength && value.All(c => c >= 0x20 && c <= 0x7E);

    private static bool IsAbsoluteHttpUrl(string value, int maxLength) =>
        value.Length <= maxLength
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

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

        if (string.IsNullOrWhiteSpace(requirements.Amount))
        {
            throw new InvalidOperationException($"Route '{routePath}': Amount is required.");
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

        if (!X402RequirementSupport.IsAuthorizationFlow(requirements) && !X402RequirementSupport.IsUpfrontFlow(requirements))
        {
            var paymentFlow = ExactSchemeExtra.FromRequirements(requirements)?.PaymentFlow;
            throw new InvalidOperationException(
                $"Route '{routePath}': paymentFlow '{paymentFlow}' is not supported; this middleware implements the authorization and upfront flows.");
        }
    }
}
