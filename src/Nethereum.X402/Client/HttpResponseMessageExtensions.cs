using Nethereum.Documentation;
using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.Client;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "HttpResponseMessageExtensions - read PAYMENT-RESPONSE from a response")]
public static class HttpResponseMessageExtensions
{
    public static SettlementResponse? GetSettlementResponse(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));

        if (!response.Headers.TryGetValues(X402Headers.PaymentResponse, out var values))
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
            var json = Encoding.UTF8.GetString(X402Base64.Decode(base64Header));
            return JsonSerializer.Deserialize<SettlementResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    public static bool HasPaymentResponse(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));
        return response.Headers.Contains(X402Headers.PaymentResponse);
    }

    public static string? GetTransactionHash(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Transaction;
    }

    public static string? GetPayerAddress(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Payer;
    }

    public static bool IsPaymentSuccessful(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Success == true;
    }

    public static string? GetPaymentError(this HttpResponseMessage response)
    {
        var settlement = response.GetSettlementResponse();
        return settlement?.Success == false ? settlement.ErrorReason : null;
    }
}
