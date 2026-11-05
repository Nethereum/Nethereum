using Nethereum.Documentation;
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

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient - pays 402 responses automatically or from explicit requirements")]
public class X402HttpClient
{
    private readonly HttpClient _httpClient;
    private readonly TransferWithAuthorisationBuilder _builder;
    private readonly TransferWithAuthorisationSigner _signer;

    private readonly string _privateKey;
    private readonly EthECKey _key;
    private readonly X402HttpClientOptions? _options;

    private const int Permit2ValidAfterBackdateSeconds = 600;

    public string Address { get; }

    public X402HttpClient(
        HttpClient httpClient,
        string privateKey)
        : this(httpClient, privateKey, null, false)
    {
    }

    public X402HttpClient(
        HttpClient httpClient,
        string privateKey,
        X402HttpClientOptions options)
        : this(httpClient, privateKey, options, true)
    {
    }

    private X402HttpClient(
        HttpClient httpClient,
        string privateKey,
        X402HttpClientOptions? options,
        bool optionsRequired)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        if (optionsRequired)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));
            options.Validate();
        }
        _options = options;

        _builder = new TransferWithAuthorisationBuilder();
        _signer = new TransferWithAuthorisationSigner();

        _key = new EthECKey(_privateKey.EnsureHexPrefix().Substring(2));
        Address = _key.GetPublicAddress();
    }

    public async Task<HttpResponseMessage> GetAsync(
        string uri,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        ArgumentNullException.ThrowIfNull(requirements, nameof(requirements));

        var paymentPayload = await CreateSignedPaymentAsync(requirements);

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add(X402Headers.PaymentSignature, EncodePaymentHeader(paymentPayload));

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static string EncodePaymentHeader(PaymentPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static PaymentPayload DecodePaymentHeader(string header)
    {
        var json = Encoding.UTF8.GetString(X402Base64.Decode(header));
        return JsonSerializer.Deserialize<PaymentPayload>(json)!;
    }

    public async Task<HttpResponseMessage> GetAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

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

    public async Task<HttpResponseMessage> DeleteAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureAutomaticMode();
        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        return await SendWithAutomaticPaymentAsync(request, cancellationToken);
    }

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
        var clonedRequest = await CloneRequestAsync(request);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_httpClient.Timeout != Timeout.InfiniteTimeSpan)
            deadline.CancelAfter(_httpClient.Timeout);

        PaymentRequired? paymentRequired;
        try
        {
            var response = await _httpClient.SendAsync(clonedRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            if (response.StatusCode != HttpStatusCode.PaymentRequired)
            {
                try
                {
                    await response.Content.LoadIntoBufferAsync(_httpClient.MaxResponseContentBufferSize).WaitAsync(deadline.Token);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }

                return response;
            }

            try
            {
                paymentRequired = await ReadPaymentRequiredAsync(response, deadline.Token);
            }
            finally
            {
                response.Dispose();
            }
        }
        catch (OperationCanceledException ex) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException(
                $"The request was canceled due to the configured HttpClient.Timeout of {_httpClient.Timeout.TotalSeconds} seconds elapsing.",
                new TimeoutException("The operation was canceled.", ex));
        }

        if (paymentRequired?.Accepts == null || !paymentRequired.Accepts.Any())
        {
            throw new InvalidOperationException("Server returned 402 but no payment requirements were provided");
        }

        var selectedRequirements = _options!.Selector.SelectRequirements(
            paymentRequired.Accepts,
            _options.PreferredNetwork,
            _options.PreferredScheme);

        EnsureWithinMaximum(selectedRequirements);

        _options.Policy?.Assert(selectedRequirements);

        if (request.Headers.Contains(X402Headers.PaymentSignature))
        {
            throw new InvalidOperationException(
                "Request already contains PAYMENT-SIGNATURE header but server returned 402. " +
                "This may indicate payment was rejected or already used.");
        }

        var paymentPayload = await CreateSignedPaymentAsync(selectedRequirements, paymentRequired.Extensions);
        paymentPayload.Resource = paymentRequired.Resource;

        var paidRequest = await CloneRequestAsync(request);
        paidRequest.Headers.Add(X402Headers.PaymentSignature, EncodePaymentHeader(paymentPayload));

        return await _httpClient.SendAsync(paidRequest, cancellationToken);
    }

    private async Task<PaymentRequired?> ReadPaymentRequiredAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string? paymentRequiredJson = null;
        if (response.Headers.TryGetValues(X402Headers.PaymentRequired, out var headerValues))
        {
            try { paymentRequiredJson = Encoding.UTF8.GetString(X402Base64.Decode(X402BoundedBody.Bound(headerValues.First(), _options!.MaxResponseBytes))); }
            catch (FormatException) { paymentRequiredJson = null; }
        }
        if (paymentRequiredJson == null)
        {
            paymentRequiredJson = await X402BoundedBody.ReadAsync(response, _options!.MaxResponseBytes, cancellationToken);
        }
        return JsonSerializer.Deserialize<PaymentRequired>(paymentRequiredJson, jsonOptions);
    }

    private void EnsureWithinMaximum(PaymentRequirements selectedRequirements)
    {
        var maxAtomic = BigInteger.Parse(_options!.MaxAmount);
        if (!X402Number.TryParse(selectedRequirements.Amount, out var atomicUnits) || atomicUnits > maxAtomic)
        {
            throw new X402PaymentExceedsMaximumException(selectedRequirements.Amount ?? "", _options.MaxAmount);
        }
    }

    private async Task<PaymentPayload> CreateSignedPaymentAsync(PaymentRequirements requirements, object? declaredExtensions = null)
    {
        if (!X402RequirementSupport.IsSupportedByThisLibrary(requirements))
        {
            throw new InvalidOperationException(
                "Payment requirement is not supported by this client: only the exact scheme with the authorization or upfront " +
                "payment flow and the eip3009 or permit2 asset transfer method can be signed.");
        }

        if (X402RequirementSupport.ResolveAssetTransferMethod(requirements) == X402AssetTransferMethods.Permit2)
        {
            return await CreatePermit2PaymentAsync(requirements, declaredExtensions);
        }

        return await CreateEip3009PaymentAsync(requirements, declaredExtensions);
    }

    private async Task<PaymentPayload> CreateEip3009PaymentAsync(PaymentRequirements requirements, object? declaredExtensions)
    {
        var extra = ExactSchemeExtra.FromRequirements(requirements);

        if (extra == null)
            throw new InvalidOperationException(
                "Payment requirement is missing the exact-scheme extra (token name/version).");
        var chainId = Caip2.ParseEip155ChainId(requirements.Network);

        var validBefore = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ValiditySeconds(requirements));
        var authorization = _builder.BuildFromPaymentRequirements(requirements, Address, null, validBefore);
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
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = signature.CreateStringSignature(),
                Authorization = authorization
            },
            Extensions = declaredExtensions
        };
    }

    private async Task<PaymentPayload> CreatePermit2PaymentAsync(PaymentRequirements requirements, object? declaredExtensions)
    {
        var chainId = Caip2.ParseEip155ChainId(requirements.Network);

        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + ValiditySeconds(requirements));
        var validAfter = new BigInteger(now - Permit2ValidAfterBackdateSeconds);

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
            authorization.ToWitnessMessage(), chainId, X402Permit2Addresses.Permit2, _key);

        var sponsoringInfo = await new X402GasSponsoringSigner(_key, Address, _options)
            .TrySignAsync(requirements, declaredExtensions, chainId, deadline);

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = X402ExtensionMerge.Merge(declaredExtensions, sponsoringInfo)
        };
    }

    private long ValiditySeconds(PaymentRequirements requirements)
    {
        var cap = _options?.MaxAuthorizationValiditySeconds ?? X402HttpClientOptions.DefaultMaxAuthorizationValiditySeconds;
        return requirements.MaxTimeoutSeconds > 0 ? Math.Min(requirements.MaxTimeoutSeconds, cap) : cap;
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

        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

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
}
