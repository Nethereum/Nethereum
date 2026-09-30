using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nethereum.WebAuthn.Windows
{
    [SupportedOSPlatform("windows")]
    internal static class NativeWebAuthn
    {
        private const string DllName = "webauthn.dll";

        internal const string CredentialTypePublicKey = "public-key";
        internal const string HashAlgorithmSha256 = "SHA-256";

        // webauthn.h: WEBAUTHN_COSE_ALGORITHM_ECDSA_P256_WITH_SHA256 (COSE alg -7, RFC 9053 Table 1).
        internal const int CoseAlgorithmEcdsaP256WithSha256 = -7;

        internal const uint AuthenticatorAttachmentPlatform = 1;

        internal const uint UserVerificationRequirementRequired = 1;
        internal const uint UserVerificationRequirementPreferred = 2;

        internal const uint AttestationConveyancePreferenceNone = 1;

        internal const uint RpEntityInformationCurrentVersion = 1;
        internal const uint UserEntityInformationCurrentVersion = 1;
        internal const uint ClientDataCurrentVersion = 1;
        internal const uint CoseCredentialParameterCurrentVersion = 1;
        internal const uint CredentialCurrentVersion = 1;

        internal const uint MakeCredentialOptionsVersion1 = 1;
        internal const uint GetAssertionOptionsVersion1 = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_RP_ENTITY_INFORMATION
        {
            public uint dwVersion;
            public string pwszId;
            public string pwszName;
            public string? pwszIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_USER_ENTITY_INFORMATION
        {
            public uint dwVersion;
            public uint cbId;
            public IntPtr pbId;
            public string pwszName;
            public string? pwszIcon;
            public string pwszDisplayName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_CLIENT_DATA
        {
            public uint dwVersion;
            public uint cbClientDataJSON;
            public IntPtr pbClientDataJSON;
            public string pwszHashAlgId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_COSE_CREDENTIAL_PARAMETER
        {
            public uint dwVersion;
            public string pwszCredentialType;
            public int lAlg;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WEBAUTHN_COSE_CREDENTIAL_PARAMETERS
        {
            public uint cCredentialParameters;
            public IntPtr pCredentialParameters;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_CREDENTIAL
        {
            public uint dwVersion;
            public uint cbId;
            public IntPtr pbId;
            public string pwszCredentialType;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WEBAUTHN_CREDENTIALS
        {
            public uint cCredentials;
            public IntPtr pCredentials;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WEBAUTHN_EXTENSIONS
        {
            public uint cExtensions;
            public IntPtr pExtensions;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
        {
            public uint dwVersion;
            public uint dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint dwAuthenticatorAttachment;
            public int bRequireResidentKey;
            public uint dwUserVerificationRequirement;
            public uint dwAttestationConveyancePreference;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
        {
            public uint dwVersion;
            public uint dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint dwAuthenticatorAttachment;
            public uint dwUserVerificationRequirement;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_CREDENTIAL_ATTESTATION
        {
            public uint dwVersion;
            public string pwszFormatType;
            public uint cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public uint cbAttestation;
            public IntPtr pbAttestation;
            public uint dwAttestationDecodeType;
            public IntPtr pvAttestationDecode;
            public uint cbAttestationObject;
            public IntPtr pbAttestationObject;
            public uint cbCredentialId;
            public IntPtr pbCredentialId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WEBAUTHN_ASSERTION
        {
            public uint dwVersion;
            public uint cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public uint cbSignature;
            public IntPtr pbSignature;
            public WEBAUTHN_CREDENTIAL Credential;
            public uint cbUserId;
            public IntPtr pbUserId;
        }

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern int WebAuthNAuthenticatorMakeCredential(
            IntPtr hWnd,
            ref WEBAUTHN_RP_ENTITY_INFORMATION pRpInformation,
            ref WEBAUTHN_USER_ENTITY_INFORMATION pUserInformation,
            ref WEBAUTHN_COSE_CREDENTIAL_PARAMETERS pPubKeyCredParams,
            ref WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
            ref WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS pWebAuthNMakeCredentialOptions,
            out IntPtr ppWebAuthNCredentialAttestation);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern int WebAuthNAuthenticatorGetAssertion(
            IntPtr hWnd,
            string pwszRpId,
            ref WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
            ref WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS pWebAuthNGetAssertionOptions,
            out IntPtr ppWebAuthNAssertion);

        [DllImport(DllName)]
        internal static extern void WebAuthNFreeCredentialAttestation(IntPtr pWebAuthNCredentialAttestation);

        [DllImport(DllName)]
        internal static extern void WebAuthNFreeAssertion(IntPtr pWebAuthNAssertion);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern IntPtr WebAuthNGetErrorName(int hr);
    }
}
