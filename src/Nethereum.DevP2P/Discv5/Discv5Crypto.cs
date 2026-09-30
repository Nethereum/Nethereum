using System;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;

namespace Nethereum.DevP2P.Discv5
{
    public static class Discv5Crypto
    {
        public static byte[] ComputeNodeId(byte[] pubKey)
        {
            if (pubKey == null) throw new ArgumentNullException(nameof(pubKey));
            return new Sha3Keccack().CalculateHash(NormalizeToXy(pubKey));
        }

        private static byte[] NormalizeToXy(byte[] pubKey)
        {
            if (pubKey.Length == 64) return pubKey;
            if (pubKey.Length == 33) return DecompressToXy(pubKey);
            if (pubKey.Length == 65 && pubKey[0] == 0x04)
            {
                var xy = new byte[64];
                Buffer.BlockCopy(pubKey, 1, xy, 0, 64);
                return xy;
            }
            throw new ArgumentException($"Unsupported pubkey length {pubKey.Length}");
        }

        public static byte[] DecompressToXy(byte[] compressed)
        {
            if (compressed == null || compressed.Length != 33)
                throw new ArgumentException("compressed pubkey must be 33 bytes");
            var q = ECKey.Secp256k1.Curve.DecodePoint(compressed).Normalize();
            var x = q.AffineXCoord.GetEncoded();
            var y = q.AffineYCoord.GetEncoded();
            var xy = new byte[64];
            Buffer.BlockCopy(LeftPad(x, 32), 0, xy, 0, 32);
            Buffer.BlockCopy(LeftPad(y, 32), 0, xy, 32, 32);
            return xy;
        }

        public static byte[] CompressXy(byte[] xy)
        {
            if (xy == null || xy.Length != 64)
                throw new ArgumentException("uncompressed pubkey must be 64 bytes (x||y)");
            var prefixed = new byte[65];
            prefixed[0] = 0x04;
            Buffer.BlockCopy(xy, 0, prefixed, 1, 64);
            var q = ECKey.Secp256k1.Curve.DecodePoint(prefixed).Normalize();
            return q.GetEncoded(true);
        }

        public static byte[] EcdhCompressed(EthECKey localPrivateKey, byte[] remoteCompressedPubKey)
            => localPrivateKey.CalculateEcdhSharedPointCompressed(remoteCompressedPubKey);

        public static byte[] SignIdSignature(EthECKey localKey, byte[] inputHash)
        {
            if (localKey == null) throw new ArgumentNullException(nameof(localKey));
            if (inputHash == null || inputHash.Length == 0)
                throw new ArgumentException("input hash must be non-empty", nameof(inputHash));

            var privateKeyParams = new ECPrivateKeyParameters(
                new BigInteger(1, localKey.GetPrivateKeyAsBytes()),
                ECKey.CURVE);

            var signer = new ECDsaSigner(new HMacDsaKCalculator(new Sha256Digest()));
            signer.Init(true, privateKeyParams);
            var signature = signer.GenerateSignature(inputHash);

            var canonical = new ECDSASignature(signature).MakeCanonical();

            var sigRs = new byte[64];
            Buffer.BlockCopy(LeftPad(canonical.R.ToByteArrayUnsigned(), 32), 0, sigRs, 0, 32);
            Buffer.BlockCopy(LeftPad(canonical.S.ToByteArrayUnsigned(), 32), 0, sigRs, 32, 32);
            return sigRs;
        }

        public static bool VerifyIdSignature(byte[] sigRs, byte[] inputHash, byte[] signerPubKey)
        {
            if (sigRs == null || sigRs.Length != 64) return false;
            if (signerPubKey == null) return false;
            var r = new BigInteger(1, sigRs, 0, 32);
            var s = new BigInteger(1, sigRs, 32, 32);

            byte[] xy;
            try { xy = NormalizeToXy(signerPubKey); }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }

            try
            {
                var prefixed = new byte[65];
                prefixed[0] = 0x04;
                Buffer.BlockCopy(xy, 0, prefixed, 1, 64);
                var q = ECKey.Secp256k1.Curve.DecodePoint(prefixed);
                if (q == null || q.IsInfinity || !q.IsValid()) return false;
                var pubParams = new ECPublicKeyParameters("EC", q, ECKey.CURVE);

                var signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner();
                signer.Init(false, pubParams);
                return signer.VerifySignature(inputHash, r, s);
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static byte[] LeftPad(byte[] bytes, int length)
        {
            if (bytes.Length == length) return bytes;
            if (bytes.Length > length)
            {
                var trimmed = new byte[length];
                Buffer.BlockCopy(bytes, bytes.Length - length, trimmed, 0, length);
                return trimmed;
            }
            var padded = new byte[length];
            Buffer.BlockCopy(bytes, 0, padded, length - bytes.Length, bytes.Length);
            return padded;
        }
    }
}
