using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.Client;

/// <summary>
/// Extension methods for HttpResponseMessage to parse x402 payment responses.
/// Spec Reference: Section 5.3 - X-PAYMENT-RESPONSE Header
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Gets the settlement response from the X-PAYMENT-RESPONSE header if present.
    /// Returns null if no payment response header exists.
    /// Spec Reference: Section 5.3 - Settlement Response Format
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>Settlement response details, or null if header not present</returns>
    public static SettlementResponse? GetSettlementResponse(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));

        if (!response.Headers.TryGetValues("X-PAYMENT-RESPONSE", out var values))
        {
            return null;
        }

        var base64Header = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(base64Header))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Header));
            return JsonSerializer.Deserialize<SettlementResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Invalid base64 or JSON - return null rather than throwing
            return null;
        }
    }

    /// <summary>
    /// Checks if the response contains an X-PAYMENT-RESPONSE header.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>True if X-PAYMENT-RESPONSE header is present</returns>
    public static bool HasPaymentResponse(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));
        return response.Headers.Contains("X-PAYMENT-RESPONSE");
    }

    /// <summary>
    /// Gets the transaction hash from the settlement response if available.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>Transaction hash, or null if not available</returns>
    public static string? GetTransactionHash(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Transaction;
    }

    /// <summary>
    /// Gets the payer address from the settlement response if available.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>Payer address, or null if not available</returns>
    public static string? GetPayerAddress(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Payer;
    }

    /// <summary>
    /// Checks if the payment was successfully settled.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>True if payment was settled successfully, false if failed or no payment</returns>
    public static bool IsPaymentSuccessful(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Success == true;
    }

    /// <summary>
    /// Gets the error reason if payment settlement failed.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>Error reason, or null if payment succeeded or no payment</returns>
    public static string? GetPaymentError(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Success == false ? settlement.ErrorReason : null;
    }
}
