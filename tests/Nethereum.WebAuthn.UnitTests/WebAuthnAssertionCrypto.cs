using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Nethereum.WebAuthn.UnitTests
{
    public static class WebAuthnAssertionCrypto
    {
        public static byte[] To32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        public static byte[] DigestOf(byte[] authenticatorData, string clientDataJson)
        {
            var clientDataHash = SHA256.HashData(Encoding.UTF8.GetBytes(clientDataJson));
            var signed = new byte[authenticatorData.Length + clientDataHash.Length];
            Buffer.BlockCopy(authenticatorData, 0, signed, 0, authenticatorData.Length);
            Buffer.BlockCopy(clientDataHash, 0, signed, authenticatorData.Length, clientDataHash.Length);
            return SHA256.HashData(signed);
        }

        public static bool Verify(BigInteger pubKeyX, BigInteger pubKeyY, byte[] authenticatorData, string clientDataJson, byte[] r, byte[] s)
        {
            var digest = DigestOf(authenticatorData, clientDataJson);

            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = To32(pubKeyX), Y = To32(pubKeyY) }
            };

            using var ecdsa = ECDsa.Create(parameters);

            var signature = new byte[64];
            Buffer.BlockCopy(r, 0, signature, 0, 32);
            Buffer.BlockCopy(s, 0, signature, 32, 32);

            return ecdsa.VerifyHash(digest, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
    }
}
