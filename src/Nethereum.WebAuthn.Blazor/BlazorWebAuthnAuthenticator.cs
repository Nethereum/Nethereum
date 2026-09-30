using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Nethereum.WebAuthn.Blazor
{
    public class BlazorWebAuthnAuthenticator : IWebAuthnAuthenticator, IWebAuthnCredentialFactory, IAsyncDisposable
    {
        private const string ModulePath = "./_content/Nethereum.WebAuthn.Blazor/nethereumWebAuthn.js";

        private readonly IJSRuntime _jsRuntime;
        private readonly bool _requireUserVerificationForAssertion;
        private Task<IJSObjectReference>? _moduleTask;

        public BlazorWebAuthnAuthenticator(IJSRuntime jsRuntime, bool requireUserVerificationForAssertion = true)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _requireUserVerificationForAssertion = requireUserVerificationForAssertion;
        }

        private Task<IJSObjectReference> ModuleAsync() =>
            _moduleTask ??= _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

        public async Task<WebAuthnCreatedCredential> CreateCredentialAsync(WebAuthnCredentialCreationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var module = await ModuleAsync().ConfigureAwait(false);

            var userId = options.UserId ?? Guid.NewGuid().ToByteArray();
            var challenge = options.Challenge ?? RandomNumberGenerator.GetBytes(32);

            var requestJson = JsonSerializer.Serialize(new CreateCredentialRequest
            {
                RpId = options.RpId,
                RpName = options.RpName,
                UserName = options.UserName,
                UserIdB64Url = Base64UrlEncoder.Encode(userId),
                ChallengeB64Url = Base64UrlEncoder.Encode(challenge),
                RequireUserVerification = options.RequireUserVerification
            });

            var responseJson = await module.InvokeAsync<string>("createCredential", requestJson).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<CreateCredentialResponse>(responseJson)
                ?? throw new InvalidOperationException("createCredential returned no data.");

            var (x, y) = ResolvePublicKey(response);

            return new WebAuthnCreatedCredential
            {
                PlatformCredentialId = Base64UrlEncoder.Decode(response.RawId),
                PubKeyX = x,
                PubKeyY = y,
                RequireUserVerification = options.RequireUserVerification
            };
        }

        public async Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId)
        {
            var module = await ModuleAsync().ConfigureAwait(false);

            var responseJson = await module.InvokeAsync<string>(
                "getAssertion",
                Base64UrlEncoder.Encode(challenge),
                Base64UrlEncoder.Encode(credentialId),
                rpId,
                _requireUserVerificationForAssertion).ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<GetAssertionResponse>(responseJson)
                ?? throw new InvalidOperationException("getAssertion returned no data.");

            var (r, s) = WebAuthnResponseParser.DecodeDerEcdsaSignatureToLowS(Convert.FromBase64String(response.Signature));

            return new WebAuthnAssertion
            {
                CredentialId = credentialId,
                AuthenticatorData = Convert.FromBase64String(response.AuthenticatorData),
                ClientDataJSON = response.ClientDataJSON,
                R = r,
                S = s
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (_moduleTask != null)
            {
                var module = await _moduleTask.ConfigureAwait(false);
                await module.DisposeAsync().ConfigureAwait(false);
                _moduleTask = null;
            }
        }

        private static (BigInteger x, BigInteger y) ResolvePublicKey(CreateCredentialResponse response)
        {
            if (response.PublicKeySpki != null)
            {
                return WebAuthnResponseParser.DecodeP256PublicKeyFromSpki(Convert.FromBase64String(response.PublicKeySpki));
            }

            var authenticatorData = response.AuthenticatorData != null
                ? Convert.FromBase64String(response.AuthenticatorData)
                : WebAuthnResponseParser.ExtractAuthDataFromAttestationObject(Convert.FromBase64String(response.AttestationObject));

            var cosePublicKey = WebAuthnResponseParser.ExtractCosePublicKeyFromAuthenticatorData(authenticatorData);
            return WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cosePublicKey);
        }

        private class CreateCredentialRequest
        {
            [JsonPropertyName("rpId")] public string RpId { get; set; } = "";
            [JsonPropertyName("rpName")] public string RpName { get; set; } = "";
            [JsonPropertyName("userName")] public string UserName { get; set; } = "";
            [JsonPropertyName("userIdB64Url")] public string UserIdB64Url { get; set; } = "";
            [JsonPropertyName("challengeB64Url")] public string ChallengeB64Url { get; set; } = "";
            [JsonPropertyName("requireUserVerification")] public bool RequireUserVerification { get; set; }
        }

        private class CreateCredentialResponse
        {
            [JsonPropertyName("rawId")] public string RawId { get; set; } = "";
            [JsonPropertyName("publicKeySpki")] public string? PublicKeySpki { get; set; }
            [JsonPropertyName("authenticatorData")] public string? AuthenticatorData { get; set; }
            [JsonPropertyName("attestationObject")] public string AttestationObject { get; set; } = "";
        }

        private class GetAssertionResponse
        {
            [JsonPropertyName("authenticatorData")] public string AuthenticatorData { get; set; } = "";
            [JsonPropertyName("clientDataJSON")] public string ClientDataJSON { get; set; } = "";
            [JsonPropertyName("signature")] public string Signature { get; set; } = "";
        }
    }
}
