using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.Client;

/// <summary>
/// HTTP client for making x402-paid requests.
/// Supports both manual payment flow (explicit PaymentRequirements) and
/// automatic payment flow (handles 402 responses automatically).
/// Spec Reference: Section 4 - Client Flow
/// </summary>
public class X402HttpClient
{
    private readonly HttpClient _httpClient;
    private readonly TransferWithAuthorisationBuilder _builder;
    private readonly TransferWithAuthorisationSigner _signer;
    private readonly string _privateKey;
    private readonly string _tokenName;
    private readonly string _tokenVersion;
    private readonly int _chainId;
    private readonly string _tokenAddress;
    private readonly X402HttpClientOptions? _options;

    public string Address { get; }

    /// <summary>
    /// Creates a new X402HttpClient for manual payment flow.
    /// User must explicitly provide PaymentRequirements when making requests.
    /// </summary>
    public X402HttpClient(
        HttpClient httpClient,
        string privateKey,
        string tokenName,
        string tokenVersion,
        int chainId,
        string tokenAddress)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        _tokenName = tokenName ?? throw new ArgumentNullException(nameof(tokenName));
        _tokenVersion = tokenVersion ?? throw new ArgumentNullException(nameof(tokenVersion));
        _chainId = chainId;
        _tokenAddress = tokenAddress ?? throw new ArgumentNullException(nameof(tokenAddress));
        _options = null;

        _builder = new TransferWithAuthorisationBuilder();
        _signer = new TransferWithAuthorisationSigner();

        // Derive address from private key
        var key = new Nethereum.Signer.EthECKey(_privateKey.EnsureHexPrefix().Substring(2));
        Address = key.GetPublicAddress();
    }

    /// <summary>
    /// Creates a new X402HttpClient with automatic payment flow.
    /// Automatically handles 402 responses by creating and sending payment.
    /// </summary>
    public X402HttpClient(
        HttpClient httpClient,
        string privateKey,
        X402HttpClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        options.Validate();
        _options = options;

        _tokenName = options.TokenName;
        _tokenVersion = options.TokenVersion;
        _chainId = options.ChainId;
        _tokenAddress = options.TokenAddress;

        _builder = new TransferWithAuthorisationBuilder();
        _signer = new TransferWithAuthorisationSigner();

        // Derive address from private key
        var key = new Nethereum.Signer.EthECKey(_privateKey.EnsureHexPrefix().Substring(2));
        Address = key.GetPublicAddress();
    }

    /// <summary>
    /// Makes a GET request with x402 payment.
    /// Spec Reference: Section 4.3 - Payment Submission
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(
        string uri,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        // Build authorization using builder
        var authorization = _builder.BuildFromPaymentRequirements(requirements, Address);

        // Sign using consolidated signer
        var signature = await _signer.SignWithPrivateKeyAsync(
            authorization,
            _tokenName,
            _tokenVersion,
            _chainId,
            _tokenAddress,
            _privateKey
        );

        // Encode signature to hex using Nethereum extension
        var signatureHex = signature.CreateStringSignature();

        // Create payment payload
        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = requirements.Scheme,
            Network = requirements.Network,
            Payload = new ExactSchemePayload
            {
                Signature = signatureHex,
                Authorization = authorization
            }
        };

        // Encode and send request
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-PAYMENT", EncodePaymentHeader(paymentPayload));

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Encodes a payment payload to base64 for X-PAYMENT header.
    /// Spec Reference: Section 5.2 - Payment Payload Format
    /// </summary>
    private static string EncodePaymentHeader(PaymentPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Decodes a payment header from base64.
    /// </summary>
    public static PaymentPayload DecodePaymentHeader(string header)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(header));
        return JsonSerializer.Deserialize<PaymentPayload>(json)!;
    }

    #region Automatic Payment Flow Methods

    /// <summary>
    /// Makes a GET request with automatic payment handling.
    /// If server responds with 402 Payment Required, automatically creates and sends payment.
    /// Spec Reference: Section 4 - Complete Client Flow
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Makes a POST request with automatic payment handling.
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync(
        string uri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = content
        };
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Makes a PUT request with automatic payment handling.
    /// </summary>
    public async Task<HttpResponseMessage> PutAsync(
        string uri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = content
        };
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Makes a DELETE request with automatic payment handling.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends an HTTP request with automatic payment handling.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        EnsureAutomaticMode();
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithAutomaticPaymentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Step 1: Make initial request
        var clonedRequest = await CloneRequestAsync(request);
        var response = await _httpClient.SendAsync(clonedRequest, cancellationToken);

        // Step 2: If not 402, return immediately
        if (response.StatusCode != HttpStatusCode.PaymentRequired)
        {
            return response;
        }

        // Step 3: Parse payment requirements
        var paymentRequiredJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var paymentRequired = JsonSerializer.Deserialize<PaymentRequirementsResponse>(
            paymentRequiredJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (paymentRequired?.Accepts == null || !paymentRequired.Accepts.Any())
        {
            throw new InvalidOperationException("Server returned 402 but no payment requirements were provided");
        }

        // Step 4: Select requirements using selector
        var selectedRequirements = _options!.Selector.SelectRequirements(
            paymentRequired.Accepts,
            _options.PreferredNetwork,
            _options.PreferredScheme);

        // Step 5: Validate payment amount
        if (decimal.TryParse(selectedRequirements.MaxAmountRequired, out var atomicUnits))
        {
            var amountUsdc = atomicUnits / 1_000_000m; // Convert from atomic units (6 decimals for USDC)
            if (amountUsdc > _options.MaxPaymentAmount)
            {
                throw new X402PaymentExceedsMaximumException(amountUsdc, _options.MaxPaymentAmount);
            }
        }

        // Step 6: Prevent infinite retry - check if request already has payment
        if (request.Headers.Contains("X-PAYMENT"))
        {
            throw new InvalidOperationException(
                "Request already contains X-PAYMENT header but server returned 402. " +
                "This may indicate payment was rejected or already used.");
        }

        // Step 7: Create payment using existing manual flow logic
        var authorization = _builder.BuildFromPaymentRequirements(selectedRequirements, Address);

        var signature = await _signer.SignWithPrivateKeyAsync(
            authorization,
            _tokenName,
            _tokenVersion,
            _chainId,
            _tokenAddress,
            _privateKey
        );

        var signatureHex = signature.CreateStringSignature();

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = selectedRequirements.Scheme,
            Network = selectedRequirements.Network,
            Payload = new ExactSchemePayload
            {
                Signature = signatureHex,
                Authorization = authorization
            }
        };

        // Step 8: Clone original request and add payment header
        var paidRequest = await CloneRequestAsync(request);
        paidRequest.Headers.Add("X-PAYMENT", EncodePaymentHeader(paymentPayload));

        // Step 9: Retry with payment
        return await _httpClient.SendAsync(paidRequest, cancellationToken);
    }

    private void EnsureAutomaticMode()
    {
        if (_options == null)
        {
            throw new InvalidOperationException(
                "This method requires automatic payment mode. " +
                "Use the constructor that accepts X402HttpClientOptions, or use GetAsync(uri, requirements) for manual payment flow.");
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Clone content if present
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            // Clone content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Clone request headers (except X-PAYMENT which we'll add fresh)
        foreach (var header in request.Headers)
        {
            if (header.Key != "X-PAYMENT")
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        clone.Version = request.Version;

        return clone;
    }

    #endregion
}
