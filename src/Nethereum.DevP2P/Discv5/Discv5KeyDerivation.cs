using System;
using System.Security.Cryptography;

namespace Nethereum.DevP2P.Discv5
{
    public static class Discv5KeyDerivation
    {
        public static readonly byte[] KeyAgreementInfo =
            System.Text.Encoding.ASCII.GetBytes("discovery v5 key agreement");

        public static readonly byte[] IdSignatureText =
            System.Text.Encoding.ASCII.GetBytes("discovery v5 identity proof");

        public static (byte[] InitiatorKey, byte[] RecipientKey) DeriveSessionKeys(
            byte[] sharedSecret, byte[] challengeData, byte[] nodeAId, byte[] nodeBId)
        {
            if (sharedSecret == null) throw new ArgumentNullException(nameof(sharedSecret));
            if (challengeData == null || challengeData.Length == 0) throw new ArgumentException("challenge-data must be non-empty", nameof(challengeData));
            if (nodeAId == null || nodeAId.Length != 32) throw new ArgumentException("node-A-id must be 32 bytes", nameof(nodeAId));
            if (nodeBId == null || nodeBId.Length != 32) throw new ArgumentException("node-B-id must be 32 bytes", nameof(nodeBId));

            var info = new byte[KeyAgreementInfo.Length + 32 + 32];
            Buffer.BlockCopy(KeyAgreementInfo, 0, info, 0, KeyAgreementInfo.Length);
            Buffer.BlockCopy(nodeAId, 0, info, KeyAgreementInfo.Length, 32);
            Buffer.BlockCopy(nodeBId, 0, info, KeyAgreementInfo.Length + 32, 32);

            var okm = HkdfSha256(sharedSecret, challengeData, info, 32);
            var initKey = new byte[16];
            var recpKey = new byte[16];
            Buffer.BlockCopy(okm, 0, initKey, 0, 16);
            Buffer.BlockCopy(okm, 16, recpKey, 0, 16);
            return (initKey, recpKey);
        }

        public static byte[] ComputeIdSignatureInput(byte[] challengeData, byte[] ephemeralPubKeyCompressed, byte[] nodeBId)
        {
            using var sha = SHA256.Create();
            var buf = new byte[IdSignatureText.Length + challengeData.Length + ephemeralPubKeyCompressed.Length + nodeBId.Length];
            int o = 0;
            Buffer.BlockCopy(IdSignatureText, 0, buf, o, IdSignatureText.Length); o += IdSignatureText.Length;
            Buffer.BlockCopy(challengeData, 0, buf, o, challengeData.Length); o += challengeData.Length;
            Buffer.BlockCopy(ephemeralPubKeyCompressed, 0, buf, o, ephemeralPubKeyCompressed.Length); o += ephemeralPubKeyCompressed.Length;
            Buffer.BlockCopy(nodeBId, 0, buf, o, nodeBId.Length);
            return sha.ComputeHash(buf);
        }

        public static byte[] HkdfSha256(byte[] ikm, byte[] salt, byte[] info, int length)
        {
            using var hmac = new HMACSHA256(salt ?? Array.Empty<byte>());
            var prk = hmac.ComputeHash(ikm);

            var output = new byte[length];
            var t = Array.Empty<byte>();
            int produced = 0;
            byte counter = 1;
            using var expandHmac = new HMACSHA256(prk);
            while (produced < length)
            {
                var inputBuf = new byte[t.Length + (info?.Length ?? 0) + 1];
                Buffer.BlockCopy(t, 0, inputBuf, 0, t.Length);
                if (info != null && info.Length > 0)
                    Buffer.BlockCopy(info, 0, inputBuf, t.Length, info.Length);
                inputBuf[inputBuf.Length - 1] = counter;
                t = expandHmac.ComputeHash(inputBuf);
                int copy = Math.Min(t.Length, length - produced);
                Buffer.BlockCopy(t, 0, output, produced, copy);
                produced += copy;
                counter++;
            }
            return output;
        }
    }
}
