using Org.BouncyCastle.Crypto;

namespace Nethereum.Signer.Crypto
{
    internal static class DeterministicDSAExtensions
    {
        public static byte[] Digest(this IDigest digest)
        {
            var result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);
            return result;
        }

        public static byte[] DoFinal(this IMac hmac)
        {
            var result = new byte[hmac.GetMacSize()];
            hmac.DoFinal(result, 0);
            return result;
        }

        public static void Update(this IMac hmac, byte[] input)
        {
            hmac.BlockUpdate(input, 0, input.Length);
        }

        public static void Update(this IDigest digest, byte[] input)
        {
            digest.BlockUpdate(input, 0, input.Length);
        }

        public static void Update(this IDigest digest, byte[] input, int offset, int length)
        {
            digest.BlockUpdate(input, offset, length);
        }
    }
}