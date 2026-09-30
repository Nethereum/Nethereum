using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Signers;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
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
    private readonly X402HttpClientOptions? _options;

    public string Address { get; }

    /// <summary>
    /// Creates a new X402HttpClient for manual payment flow. The caller provides the
    /// PaymentRequirements per request; the signing domain (token, chain, name/version) is derived
    /// from that requirement.
    /// </summary>
    public X402HttpClient(
        HttpClient httpClient,
        string privateKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
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

        var paymentPayload = await CreateSignedPaymentAsync(requirements);

        // Encode and send request
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add(X402Headers.PaymentSignature, EncodePaymentHeader(paymentPayload));

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Encodes a payment payload to base64 for PAYMENT-SIGNATURE header.
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

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string? paymentRequiredJson = null;
        if (response.Headers.TryGetValues(X402Headers.PaymentRequired, out var headerValues))
        {
            try { paymentRequiredJson = Encoding.UTF8.GetString(Convert.FromBase64String(headerValues.First())); }
            catch (FormatException) { paymentRequiredJson = null; }
        }
        if (paymentRequiredJson == null)
        {
            paymentRequiredJson = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        var paymentRequired = JsonSerializer.Deserialize<PaymentRequired>(paymentRequiredJson, jsonOptions);

        if (paymentRequired?.Accepts == null || !paymentRequired.Accepts.Any())
        {
            throw new InvalidOperationException("Server returned 402 but no payment requirements were provided");
        }

        // Step 4: Select requirements using selector
        var selectedRequirements = _options!.Selector.SelectRequirements(
            paymentRequired.Accepts,
            _options.PreferredNetwork,
            _options.PreferredScheme);

        {
            var maxAtomic = BigInteger.Parse(_options.MaxAmount);
            if (!BigInteger.TryParse(selectedRequirements.Amount, out var atomicUnits) || atomicUnits > maxAtomic)
            {
                throw new X402PaymentExceedsMaximumException(selectedRequirements.Amount ?? "", _options.MaxAmount);
            }
        }

        _options.Policy?.Assert(selectedRequirements);

        // Step 6: Prevent infinite retry - check if request already has payment
        if (request.Headers.Contains(X402Headers.PaymentSignature))
        {
            throw new InvalidOperationException(
                "Request already contains PAYMENT-SIGNATURE header but server returned 402. " +
                "This may indicate payment was rejected or already used.");
        }

        var paymentPayload = await CreateSignedPaymentAsync(selectedRequirements);

        var paidRequest = await CloneRequestAsync(request);
        paidRequest.Headers.Add(X402Headers.PaymentSignature, EncodePaymentHeader(paymentPayload));

        return await _httpClient.SendAsync(paidRequest, cancellationToken);
    }

    /// <summary>
    /// Builds and signs a payment for the given requirement. The EIP-712 signing domain is taken
    /// from the requirement: the chain ID from its CAIP-2 network, the verifying contract from its
    /// asset, and the domain name/version from its exact-scheme extra.
    /// </summary>
    private async Task<PaymentPayload> CreateSignedPaymentAsync(PaymentRequirements requirements)
    {
        var extra = ExactSchemeExtra.FromRequirements(requirements);

        if (extra?.AssetTransferMethod == X402AssetTransferMethods.Permit2)
        {
            return CreatePermit2Payment(requirements);
        }

        // default: EIP-3009 transferWithAuthorization (requires the token domain name/version).
        if (extra == null)
            throw new InvalidOperationException(
                "Payment requirement is missing the exact-scheme extra (token name/version).");
        var chainId = Caip2.ParseEip155ChainId(requirements.Network);

        var authorization = _builder.BuildFromPaymentRequirements(requirements, Address);
        var signature = await _signer.SignWithPrivateKeyAsync(
            authorization,
            extra.Name,
            extra.Version,
            chainId,
            requirements.Asset,
            _privateKey
        );

        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = signature.CreateStringSignature(),
                Authorization = authorization
            }
        };
    }

    private PaymentPayload CreatePermit2Payment(PaymentRequirements requirements)
    {
        var chainId = Caip2.ParseEip155ChainId(requirements.Network);
        var key = new EthECKey(_privateKey.EnsureHexPrefix().Substring(2));

        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timeout = requirements.MaxTimeoutSeconds > 0 ? requirements.MaxTimeoutSeconds : 3600;
        var deadline = new BigInteger(now + timeout);
        var validAfter = new BigInteger(now - 600);

        var authorization = new Permit2Authorization
        {
            From = Address,
            Permitted = new Permit2TokenPermissions { Token = requirements.Asset, Amount = requirements.Amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = nonce.ToString(),
            Deadline = deadline.ToString(),
            Witness = new Permit2WitnessData { To = requirements.PayTo, ValidAfter = validAfter.ToString() }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(), chainId, X402Permit2Addresses.Permit2, key);

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization }
        };
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

        foreach (var header in request.Headers)
        {
            if (header.Key != X402Headers.PaymentSignature)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        clone.Version = request.Version;

        return clone;
    }

    #endregion
}
