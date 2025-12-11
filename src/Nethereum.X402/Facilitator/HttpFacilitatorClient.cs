using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.Facilitator;

/// <summary>
/// HTTP implementation of facilitator client.
/// Spec Reference: Section 7 - Facilitator API
/// </summary>
public class HttpFacilitatorClient : IFacilitatorClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HttpFacilitatorClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    /// <inheritdoc />
    public async Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentPayload, nameof(paymentPayload));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        var requestBody = new
        {
            paymentPayload = paymentPayload,
            paymentRequirements = requirements
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/verify", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<VerificationResponse>(responseJson)!;
    }

    /// <inheritdoc />
    public async Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentPayload, nameof(paymentPayload));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        var requestBody = new
        {
            paymentPayload = paymentPayload,
            paymentRequirements = requirements
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/settle", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SettlementResponse>(responseJson)!;
    }

    /// <inheritdoc />
    public async Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/supported", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json)!;
    }
}
