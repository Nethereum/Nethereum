using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.Facilitator;

public class HttpFacilitatorClient : IFacilitatorClient
{
    public const string HttpClientName = "Nethereum.X402.Facilitator";

    private const int SettleTimeoutMultiplier = 3;

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HttpFacilitatorClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    public TimeSpan VerifyTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan SettleTimeoutGrace { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    public async Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentPayload, nameof(paymentPayload));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        var json = RequestJson(paymentPayload, requirements);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(VerifyTimeout);

        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await PostAsync("verify", content, bound.Token);
            var responseJson = await ReadBoundedAsync(response, bound.Token);
            if (!response.IsSuccessStatusCode)
            {
                var failed = ParseFailure<VerificationResponse>(responseJson, "isValid");
                if (failed == null)
                    response.EnsureSuccessStatusCode();

                failed!.IsValid = false;
                failed.InvalidReason ??= X402ErrorCodes.UnexpectedVerifyError;
                return failed;
            }

            return JsonSerializer.Deserialize<VerificationResponse>(responseJson)
                ?? new VerificationResponse { IsValid = false, InvalidReason = X402ErrorCodes.UnexpectedVerifyError };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The facilitator did not answer verify within {VerifyTimeout.TotalSeconds:0} seconds.", ex);
        }
    }

    public async Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentPayload, nameof(paymentPayload));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        var json = RequestJson(paymentPayload, requirements);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(SettleBound(requirements.MaxTimeoutSeconds));

        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await PostAsync("settle", content, bound.Token);
            var responseJson = await ReadBoundedAsync(response, bound.Token);
            if (!response.IsSuccessStatusCode)
            {
                var failed = ParseFailure<SettlementResponse>(responseJson, "success");
                if (failed == null)
                    response.EnsureSuccessStatusCode();

                failed!.Success = false;
                failed.ErrorReason = X402SettleOutcomes.IsDefiniteStatus(response.StatusCode) && !string.IsNullOrEmpty(failed.ErrorReason)
                    ? failed.ErrorReason
                    : X402ErrorCodes.SettlementPending;
                return failed;
            }

            return ParseFailure<SettlementResponse>(responseJson, "success")
                ?? SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, string.Empty);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, string.Empty);
        }
    }

    public async Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default)
    {
        var bound = VerifyTimeout;
        if (_httpClient.Timeout != Timeout.InfiniteTimeSpan && _httpClient.Timeout < bound)
            bound = _httpClient.Timeout;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(bound);

        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/supported", HttpCompletionOption.ResponseHeadersRead, linked.Token);
            response.EnsureSuccessStatusCode();

            var json = await ReadBoundedAsync(response, linked.Token);
            return JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json)!;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The facilitator did not answer supported within {bound.TotalSeconds:0} seconds.", ex);
        }
    }

    private Task<HttpResponseMessage> PostAsync(string path, HttpContent content, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/{path}") { Content = content };
        return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private Task<string> ReadBoundedAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        X402BoundedBody.ReadAsync(response, MaxResponseBytes, cancellationToken);

    private static T? ParseFailure<T>(string body, string discriminator) where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(discriminator, out _)
                ? document.RootElement.Deserialize<T>()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private TimeSpan SettleBound(int maxTimeoutSeconds) =>
        TimeSpan.FromSeconds((double)SettleTimeoutMultiplier * FacilitatorTransactionSender.BoundFor(maxTimeoutSeconds).TotalSeconds) + SettleTimeoutGrace;

    private static string RequestJson(PaymentPayload paymentPayload, PaymentRequirements requirements) =>
        JsonSerializer.Serialize(new
        {
            x402Version = X402Protocol.Version,
            paymentPayload,
            paymentRequirements = requirements
        });
}
