using Nethereum.Util;

namespace Nethereum.WebAuthn
{
    /// <summary>
    /// RFC 4648 §5 base64url, unpadded, as used throughout the WebAuthn spec
    /// (<c>clientDataJSON.challenge</c>, credential ids, COSE keys).
    /// </summary>
    public static class Base64UrlEncoder
    {
        public static string Encode(byte[] data) => Base64UrlConvert.Encode(data);

        public static byte[] Decode(string base64Url) => Base64UrlConvert.Decode(base64Url);
    }
}
