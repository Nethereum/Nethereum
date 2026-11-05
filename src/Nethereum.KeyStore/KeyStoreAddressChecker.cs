using System;
using Nethereum.KeyStore.Crypto;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;

namespace Nethereum.KeyStore
{
    internal static class KeyStoreAddressChecker
    {
        public static void EnsureMatches(string address, byte[] privateKey)
        {
            if (string.IsNullOrEmpty(address)) return;
            var expected = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address.Substring(2) : address;
            if (expected.Length != 40 || !IsHex(expected)) return;
            if (expected.Trim('0').Length == 0) return;
            if (!string.Equals(expected, DeriveAddress(privateKey), StringComparison.OrdinalIgnoreCase))
                throw new DecryptionException("The decrypted key does not match the address of the keystore");
        }

        private static bool IsHex(string value)
        {
            foreach (var c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        private static string DeriveAddress(byte[] privateKey)
        {
            if (privateKey == null || privateKey.Length != 32) return string.Empty;
            var curve = SecNamedCurves.GetByName("secp256k1");
            var d = new BigInteger(1, privateKey);
            if (d.SignValue == 0 || d.CompareTo(curve.N) >= 0) return string.Empty;
            var q = curve.G.Multiply(d).Normalize();
            var publicKey = new byte[64];
            var x = q.AffineXCoord.GetEncoded();
            var y = q.AffineYCoord.GetEncoded();
            Array.Copy(x, 0, publicKey, 32 - x.Length, x.Length);
            Array.Copy(y, 0, publicKey, 64 - y.Length, y.Length);
            var hash = new KeyStoreCrypto().CalculateKeccakHash(publicKey);
            var chars = new char[40];
            const string hex = "0123456789abcdef";
            for (var i = 0; i < 20; i++)
            {
                chars[2 * i] = hex[hash[12 + i] >> 4];
                chars[2 * i + 1] = hex[hash[12 + i] & 0xF];
            }
            return new string(chars);
        }
    }
}
