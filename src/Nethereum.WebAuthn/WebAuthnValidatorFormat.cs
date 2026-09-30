using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;

namespace Nethereum.WebAuthn
{
    /// <summary>
    /// The exact ABI wire format Rhinestone's <c>WebAuthnValidator</c> (core-modules) expects, for both
    /// <c>onInstall</c> credential data and the userOp/ERC-1271 signature. Mirrors
    /// <c>WebAuthnValidator.sol</c>'s <c>generateCredentialId</c>, <c>onInstall</c> and
    /// <c>_validateSignatureWithConfig</c> byte-for-byte.
    /// </summary>
    public static class WebAuthnValidatorFormat
    {
        [FunctionOutput]
        private class CredentialIdParams
        {
            [Parameter("uint256", "pubKeyX", 1)]
            public BigInteger PubKeyX { get; set; }

            [Parameter("uint256", "pubKeyY", 2)]
            public BigInteger PubKeyY { get; set; }
        }

        public class WebAuthnCredentialParam
        {
            [Parameter("uint256", "pubKeyX", 1)]
            public BigInteger PubKeyX { get; set; }

            [Parameter("uint256", "pubKeyY", 2)]
            public BigInteger PubKeyY { get; set; }

            [Parameter("bool", "requireUV", 3)]
            public bool RequireUV { get; set; }
        }

        [FunctionOutput]
        private class InstallDataParams
        {
            [Parameter("uint256", "threshold", 1)]
            public BigInteger Threshold { get; set; }

            [Parameter("tuple[]", "credentials", 2)]
            public List<WebAuthnCredentialParam> Credentials { get; set; }
        }

        public class WebAuthnAuthParam
        {
            [Parameter("bytes", "authenticatorData", 1)]
            public byte[] AuthenticatorData { get; set; }

            [Parameter("string", "clientDataJSON", 2)]
            public string ClientDataJSON { get; set; }

            [Parameter("uint256", "challengeIndex", 3)]
            public BigInteger ChallengeIndex { get; set; }

            [Parameter("uint256", "typeIndex", 4)]
            public BigInteger TypeIndex { get; set; }

            [Parameter("bytes32", "r", 5)]
            public byte[] R { get; set; }

            [Parameter("bytes32", "s", 6)]
            public byte[] S { get; set; }
        }

        [FunctionOutput]
        private class UserOpSignatureParams
        {
            [Parameter("bytes32[]", "credentialIds", 1)]
            public List<byte[]> CredentialIds { get; set; }

            [Parameter("bool", "usePrecompile", 2)]
            public bool UsePrecompile { get; set; }

            [Parameter("tuple[]", "auth", 3)]
            public List<WebAuthnAuthParam> Auth { get; set; }
        }

        public static byte[] GenerateCredentialId(BigInteger pubKeyX, BigInteger pubKeyY)
        {
            var abiEncode = new ABIEncode();
            var encoded = abiEncode.GetABIParamsEncoded(new CredentialIdParams { PubKeyX = pubKeyX, PubKeyY = pubKeyY });
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public static byte[] EncodeInstallData(BigInteger threshold, IReadOnlyList<WebAuthnCredential> credentials)
        {
            var ordered = credentials
                .Select(c => new
                {
                    Credential = c,
                    CredentialId = new BigInteger(GenerateCredentialId(c.PubKeyX, c.PubKeyY), isUnsigned: true, isBigEndian: true)
                })
                .OrderBy(c => c.CredentialId)
                .Select(c => new WebAuthnCredentialParam
                {
                    PubKeyX = c.Credential.PubKeyX,
                    PubKeyY = c.Credential.PubKeyY,
                    RequireUV = c.Credential.RequireUV
                })
                .ToList();

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIParamsEncoded(new InstallDataParams { Threshold = threshold, Credentials = ordered });
        }

        /// <summary>
        /// <c>abi.encode(bytes32[] credentialIds, bool usePrecompile, WebAuthnAuth[] auth)</c>, matching
        /// the userOp/ERC-1271 signature <c>WebAuthnValidator._validateSignatureWithConfig</c> decodes.
        /// <paramref name="credentialIds"/> and <paramref name="assertions"/> must already be in the
        /// same, ascending-by-credential-id order.
        /// </summary>
        public static byte[] EncodeUserOpSignature(IReadOnlyList<byte[]> credentialIds, bool usePrecompile, IReadOnlyList<WebAuthnAssertion> assertions)
        {
            var auth = assertions.Select(a => new WebAuthnAuthParam
            {
                AuthenticatorData = a.AuthenticatorData,
                ClientDataJSON = a.ClientDataJSON,
                ChallengeIndex = ChallengeIndexOf(a.ClientDataJSON),
                TypeIndex = TypeIndexOf(a.ClientDataJSON),
                R = a.R,
                S = a.S
            }).ToList();

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIParamsEncoded(new UserOpSignatureParams
            {
                CredentialIds = credentialIds.ToList(),
                UsePrecompile = usePrecompile,
                Auth = auth
            });
        }

        public static byte[] EncodeUserOpSignature(byte[] credentialId, bool usePrecompile, WebAuthnAssertion assertion)
        {
            return EncodeUserOpSignature(new[] { credentialId }, usePrecompile, new[] { assertion });
        }

        private static BigInteger ChallengeIndexOf(string clientDataJson)
        {
            const string marker = "\"challenge\":\"";
            var index = clientDataJson.IndexOf(marker, System.StringComparison.Ordinal);
            return index < 0 ? BigInteger.Zero : index;
        }

        private static BigInteger TypeIndexOf(string clientDataJson)
        {
            const string marker = "\"type\":\"webauthn.get\"";
            var index = clientDataJson.IndexOf(marker, System.StringComparison.Ordinal);
            return index < 0 ? BigInteger.Zero : index;
        }
    }
}
