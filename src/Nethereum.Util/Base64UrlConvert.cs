using System;

namespace Nethereum.Util
{
    /// <summary>
    /// RFC 4648 §5 base64url, unpadded. Shared by Nethereum.WebAuthn
    /// (<c>clientDataJSON.challenge</c>, credential ids, COSE keys) and
    /// Nethereum.DevP2P's EIP-1459 enrtree resolver (root signature, ENR
    /// public-key hash) — both encode the same RFC, previously as two
    /// divergent private copies.
    /// </summary>
    public static class Base64UrlConvert
    {
        public static string Encode(byte[] data)
        {
            var base64 = Convert.ToBase64String(data);
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static byte[] Decode(string base64Url)
        {
            var base64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
