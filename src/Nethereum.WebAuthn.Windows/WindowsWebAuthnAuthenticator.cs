using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.WebAuthn.Windows
{
    [SupportedOSPlatform("windows")]
    public class WindowsWebAuthnAuthenticator : IWebAuthnAuthenticator, IWebAuthnCredentialFactory
    {
        private const uint TimeoutMilliseconds = 60000;

        private readonly IWindowHandleProvider _windowHandleProvider;
        private readonly bool _requireUserVerificationForAssertion;

        public WindowsWebAuthnAuthenticator(IWindowHandleProvider? windowHandleProvider = null, bool requireUserVerificationForAssertion = true)
        {
            _windowHandleProvider = windowHandleProvider ?? new ForegroundWindowHandleProvider();
            _requireUserVerificationForAssertion = requireUserVerificationForAssertion;
        }

        public Task<WebAuthnCreatedCredential> CreateCredentialAsync(WebAuthnCredentialCreationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return Task.Run(() => CreateCredential(options));
        }

        public Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (credentialId == null) throw new ArgumentNullException(nameof(credentialId));
            if (rpId == null) throw new ArgumentNullException(nameof(rpId));
            return Task.Run(() => GetAssertion(challenge, credentialId, rpId));
        }

        private WebAuthnCreatedCredential CreateCredential(WebAuthnCredentialCreationOptions options)
        {
            var userId = options.UserId ?? Guid.NewGuid().ToByteArray();
            var challenge = options.Challenge ?? RandomNumberGenerator.GetBytes(32);
            var clientDataJson = WebAuthnNativeBufferMapping.BuildCreateClientDataJson(challenge, WebAuthnNativeBufferMapping.OriginFor(options.RpId));
            var clientDataJsonBytes = Encoding.UTF8.GetBytes(clientDataJson);

            using var scope = new NativeAllocationScope();

            var rp = new NativeWebAuthn.WEBAUTHN_RP_ENTITY_INFORMATION
            {
                dwVersion = NativeWebAuthn.RpEntityInformationCurrentVersion,
                pwszId = options.RpId,
                pwszName = options.RpName,
                pwszIcon = null
            };

            var user = new NativeWebAuthn.WEBAUTHN_USER_ENTITY_INFORMATION
            {
                dwVersion = NativeWebAuthn.UserEntityInformationCurrentVersion,
                cbId = (uint)userId.Length,
                pbId = scope.AllocBytes(userId),
                pwszName = options.UserName,
                pwszIcon = null,
                pwszDisplayName = options.UserName
            };

            var coseParametersPtr = scope.AllocStruct(new NativeWebAuthn.WEBAUTHN_COSE_CREDENTIAL_PARAMETER
            {
                dwVersion = NativeWebAuthn.CoseCredentialParameterCurrentVersion,
                pwszCredentialType = NativeWebAuthn.CredentialTypePublicKey,
                lAlg = NativeWebAuthn.CoseAlgorithmEcdsaP256WithSha256
            });
            var coseParameters = new NativeWebAuthn.WEBAUTHN_COSE_CREDENTIAL_PARAMETERS
            {
                cCredentialParameters = 1,
                pCredentialParameters = coseParametersPtr
            };

            var clientData = new NativeWebAuthn.WEBAUTHN_CLIENT_DATA
            {
                dwVersion = NativeWebAuthn.ClientDataCurrentVersion,
                cbClientDataJSON = (uint)clientDataJsonBytes.Length,
                pbClientDataJSON = scope.AllocBytes(clientDataJsonBytes),
                pwszHashAlgId = NativeWebAuthn.HashAlgorithmSha256
            };

            var makeCredentialOptions = new NativeWebAuthn.WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
            {
                dwVersion = NativeWebAuthn.MakeCredentialOptionsVersion1,
                dwTimeoutMilliseconds = TimeoutMilliseconds,
                CredentialList = default,
                Extensions = default,
                dwAuthenticatorAttachment = NativeWebAuthn.AuthenticatorAttachmentPlatform,
                bRequireResidentKey = 0,
                dwUserVerificationRequirement = ToUserVerificationRequirement(options.RequireUserVerification),
                dwAttestationConveyancePreference = NativeWebAuthn.AttestationConveyancePreferenceNone,
                dwFlags = 0
            };

            var hr = NativeWebAuthn.WebAuthNAuthenticatorMakeCredential(
                _windowHandleProvider.GetWindowHandle(),
                ref rp,
                ref user,
                ref coseParameters,
                ref clientData,
                ref makeCredentialOptions,
                out var attestationPtr);

            if (hr != 0)
            {
                throw NativeError(nameof(NativeWebAuthn.WebAuthNAuthenticatorMakeCredential), hr);
            }

            try
            {
                var attestation = Marshal.PtrToStructure<NativeWebAuthn.WEBAUTHN_CREDENTIAL_ATTESTATION>(attestationPtr);
                var authenticatorData = ReadBytes(attestation.pbAuthenticatorData, attestation.cbAuthenticatorData);
                var credentialId = ReadBytes(attestation.pbCredentialId, attestation.cbCredentialId);

                return WebAuthnNativeBufferMapping.MapCreatedCredential(authenticatorData, credentialId, options.RequireUserVerification);
            }
            finally
            {
                NativeWebAuthn.WebAuthNFreeCredentialAttestation(attestationPtr);
            }
        }

        private WebAuthnAssertion GetAssertion(byte[] challenge, byte[] credentialId, string rpId)
        {
            var clientDataJson = WebAuthnNativeBufferMapping.BuildGetClientDataJson(challenge, WebAuthnNativeBufferMapping.OriginFor(rpId));
            var clientDataJsonBytes = Encoding.UTF8.GetBytes(clientDataJson);

            using var scope = new NativeAllocationScope();

            var clientData = new NativeWebAuthn.WEBAUTHN_CLIENT_DATA
            {
                dwVersion = NativeWebAuthn.ClientDataCurrentVersion,
                cbClientDataJSON = (uint)clientDataJsonBytes.Length,
                pbClientDataJSON = scope.AllocBytes(clientDataJsonBytes),
                pwszHashAlgId = NativeWebAuthn.HashAlgorithmSha256
            };

            var allowCredentialPtr = scope.AllocStruct(new NativeWebAuthn.WEBAUTHN_CREDENTIAL
            {
                dwVersion = NativeWebAuthn.CredentialCurrentVersion,
                cbId = (uint)credentialId.Length,
                pbId = scope.AllocBytes(credentialId),
                pwszCredentialType = NativeWebAuthn.CredentialTypePublicKey
            });

            var getAssertionOptions = new NativeWebAuthn.WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
            {
                dwVersion = NativeWebAuthn.GetAssertionOptionsVersion1,
                dwTimeoutMilliseconds = TimeoutMilliseconds,
                CredentialList = new NativeWebAuthn.WEBAUTHN_CREDENTIALS
                {
                    cCredentials = 1,
                    pCredentials = allowCredentialPtr
                },
                Extensions = default,
                dwAuthenticatorAttachment = NativeWebAuthn.AuthenticatorAttachmentPlatform,
                dwUserVerificationRequirement = ToUserVerificationRequirement(_requireUserVerificationForAssertion),
                dwFlags = 0
            };

            var hr = NativeWebAuthn.WebAuthNAuthenticatorGetAssertion(
                _windowHandleProvider.GetWindowHandle(),
                rpId,
                ref clientData,
                ref getAssertionOptions,
                out var assertionPtr);

            if (hr != 0)
            {
                throw NativeError(nameof(NativeWebAuthn.WebAuthNAuthenticatorGetAssertion), hr);
            }

            try
            {
                var assertion = Marshal.PtrToStructure<NativeWebAuthn.WEBAUTHN_ASSERTION>(assertionPtr);
                var authenticatorData = ReadBytes(assertion.pbAuthenticatorData, assertion.cbAuthenticatorData);
                var derSignature = ReadBytes(assertion.pbSignature, assertion.cbSignature);

                return WebAuthnNativeBufferMapping.MapAssertion(credentialId, authenticatorData, clientDataJson, derSignature);
            }
            finally
            {
                NativeWebAuthn.WebAuthNFreeAssertion(assertionPtr);
            }
        }

        private static uint ToUserVerificationRequirement(bool requireUserVerification) =>
            requireUserVerification
                ? NativeWebAuthn.UserVerificationRequirementRequired
                : NativeWebAuthn.UserVerificationRequirementPreferred;

        private static WebAuthnNativeException NativeError(string apiName, int hr)
        {
            var errorName = Marshal.PtrToStringUni(NativeWebAuthn.WebAuthNGetErrorName(hr)) ?? "(unknown)";
            return new WebAuthnNativeException($"{apiName} failed with HRESULT 0x{hr:X8} ({errorName}).", hr);
        }

        private static byte[] ReadBytes(IntPtr ptr, uint length)
        {
            if (ptr == IntPtr.Zero || length == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, (int)length);
            return bytes;
        }

    }
}
