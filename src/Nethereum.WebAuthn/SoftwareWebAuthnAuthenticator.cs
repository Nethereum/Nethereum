using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.WebAuthn
{
    public class SoftwareWebAuthnAuthenticator : IWebAuthnAuthenticator, IWebAuthnCredentialFactory
    {
        private readonly ECDsa _key;
        private readonly byte[] _credentialId;
        private readonly bool _requireUV;
        private readonly string _origin;
        private uint _signCount;

        public byte[] CredentialId => _credentialId;

        public SoftwareWebAuthnAuthenticator(byte[] credentialId = null, bool requireUV = false, string origin = "https://nethereum.local")
        {
            _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _credentialId = credentialId ?? Guid.NewGuid().ToByteArray();
            _requireUV = requireUV;
            _origin = origin;
        }

        public (BigInteger X, BigInteger Y) GetPublicKey()
        {
            var parameters = _key.ExportParameters(false);
            return (
                new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true),
                new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true));
        }

        public Task<WebAuthnCreatedCredential> CreateCredentialAsync(WebAuthnCredentialCreationOptions options)
        {
            var (x, y) = GetPublicKey();
            return Task.FromResult(new WebAuthnCreatedCredential
            {
                PlatformCredentialId = _credentialId,
                PubKeyX = x,
                PubKeyY = y,
                RequireUserVerification = _requireUV
            });
        }

        public Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId)
        {
            _signCount++;

            var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
            var flags = (byte)(0x01 | (_requireUV ? 0x04 : 0x00));
            var signCountBytes = BitConverterBigEndian(_signCount);

            var authenticatorData = new byte[rpIdHash.Length + 1 + signCountBytes.Length];
            Buffer.BlockCopy(rpIdHash, 0, authenticatorData, 0, rpIdHash.Length);
            authenticatorData[rpIdHash.Length] = flags;
            Buffer.BlockCopy(signCountBytes, 0, authenticatorData, rpIdHash.Length + 1, signCountBytes.Length);

            var clientDataJSON = WebAuthnClientData.BuildGet(challenge, _origin);
            var clientDataHash = SHA256.HashData(Encoding.UTF8.GetBytes(clientDataJSON));

            var signedData = new byte[authenticatorData.Length + clientDataHash.Length];
            Buffer.BlockCopy(authenticatorData, 0, signedData, 0, authenticatorData.Length);
            Buffer.BlockCopy(clientDataHash, 0, signedData, authenticatorData.Length, clientDataHash.Length);
            var messageHash = SHA256.HashData(signedData);

            var signature = _key.SignHash(messageHash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var r = new byte[32];
            var s = new byte[32];
            Buffer.BlockCopy(signature, 0, r, 0, 32);
            Buffer.BlockCopy(signature, 32, s, 0, 32);
            s = WebAuthnResponseParser.NormalizeLowS(s);

            return Task.FromResult(new WebAuthnAssertion
            {
                CredentialId = credentialId,
                AuthenticatorData = authenticatorData,
                ClientDataJSON = clientDataJSON,
                R = r,
                S = s
            });
        }

        private static byte[] BitConverterBigEndian(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }
    }
}
