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
    /// <summary>
    /// Crypto helpers used by the discv5 session layer:
    /// secp256k1 pubkey (de)compression, ECDH with a compressed remote key,
    /// node-id derivation, and id-signature verification.
    /// </summary>
    public static class Discv5Crypto
    {
        /// <summary>
        /// node-id (32 bytes) = keccak256(uncompressed-pubkey-x || uncompressed-pubkey-y).
        /// Accepts either a 64-byte uncompressed pubkey (x||y) or a 33-byte
        /// compressed pubkey (prefix-byte || x).
        /// </summary>
        public static byte[] ComputeNodeId(byte[] pubKey)
        {
            if (pubKey == null) throw new ArgumentNullException(nameof(pubKey));
            byte[] xy;
            if (pubKey.Length == 64)
            {
                xy = pubKey;
            }
            else if (pubKey.Length == 33)
            {
                xy = DecompressToXy(pubKey);
            }
            else if (pubKey.Length == 65 && pubKey[0] == 0x04)
            {
                xy = new byte[64];
                Buffer.BlockCopy(pubKey, 1, xy, 0, 64);
            }
            else
            {
                throw new ArgumentException($"Unsupported pubkey length {pubKey.Length}");
            }
            return new Sha3Keccack().CalculateHash(xy);
        }

        /// <summary>
        /// Decompress a 33-byte secp256k1 compressed pubkey to its 64-byte (x||y)
        /// uncompressed form (no 0x04 prefix).
        /// </summary>
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

        /// <summary>
        /// Compress a 64-byte (x||y) uncompressed pubkey to the 33-byte form
        /// (parity-prefix || x).
        /// </summary>
        public static byte[] CompressXy(byte[] xy)
        {
            if (xy == null || xy.Length != 64)
                throw new ArgumentException("uncompressed pubkey must be 64 bytes (x||y)");
            // Reconstruct the point and re-emit compressed.
            var prefixed = new byte[65];
            prefixed[0] = 0x04;
            Buffer.BlockCopy(xy, 0, prefixed, 1, 64);
            var q = ECKey.Secp256k1.Curve.DecodePoint(prefixed).Normalize();
            return q.GetEncoded(true);
        }

        /// <summary>
        /// ECDH between our static private key and the remote's compressed
        /// ephemeral public key, returning the 33-byte compressed form of the
        /// shared point. discv5-theory.md uses this compressed point — not the
        /// bare 32-byte X-coordinate — as the IKM fed into HKDF.
        /// <para>
        /// The remote point is checked for <c>!IsInfinity &amp;&amp; IsValid()</c>
        /// after decoding. BouncyCastle's <c>DecodePoint</c> already rejects
        /// most malformed inputs, but the explicit identity/on-curve guard is
        /// the public-key validation step required by NIST SP 800-56A §5.6.2.3.3
        /// and prevents a crafted "point at infinity" from producing an all-zero
        /// shared secret.
        /// </para>
        /// </summary>
        public static byte[] EcdhCompressed(EthECKey localPrivateKey, byte[] remoteCompressedPubKey)
        {
            if (remoteCompressedPubKey == null || remoteCompressedPubKey.Length != 33)
                throw new ArgumentException("remote ephemeral pubkey must be 33 bytes (compressed)");
            var remotePoint = ECKey.Secp256k1.Curve.DecodePoint(remoteCompressedPubKey);
            RequireValidPubKeyPoint(remotePoint);
            // Cast back into Nethereum.Signer ECKey so we can pull the private
            // scalar D out without depending on reflection.
            var privateKeyParams = new Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters(
                new Org.BouncyCastle.Math.BigInteger(1, localPrivateKey.GetPrivateKeyAsBytes()),
                ECKey.CURVE);
            var sharedPoint = remotePoint.Multiply(privateKeyParams.D).Normalize();
            if (sharedPoint.IsInfinity)
                throw new ArgumentException("ECDH shared point collapsed to identity");
            return sharedPoint.GetEncoded(true);   // 33-byte compressed (parity-byte || X)
        }

        private static void RequireValidPubKeyPoint(Org.BouncyCastle.Math.EC.ECPoint q)
        {
            if (q == null || q.IsInfinity)
                throw new ArgumentException("remote pubkey is point-at-infinity");
            if (!q.IsValid())
                throw new ArgumentException("remote pubkey is not on secp256k1 curve");
        }

        /// <summary>
        /// Produce a discv5 id-signature: 64-byte (r||s) ECDSA-secp256k1 signature
        /// over <paramref name="inputHash"/> using RFC-6979 deterministic <c>k</c>
        /// (HMAC-SHA256) and low-s canonicalisation. No recovery byte.
        /// </summary>
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
            var r = signature[0];
            var s = signature[1];

            // Low-s canonicalisation: peers reject the upper half of the curve order.
            var halfOrder = ECKey.CURVE.N.ShiftRight(1);
            if (s.CompareTo(halfOrder) > 0)
                s = ECKey.CURVE.N.Subtract(s);

            var sigRs = new byte[64];
            Buffer.BlockCopy(LeftPad(r.ToByteArrayUnsigned(), 32), 0, sigRs, 0, 32);
            Buffer.BlockCopy(LeftPad(s.ToByteArrayUnsigned(), 32), 0, sigRs, 32, 32);
            return sigRs;
        }

        /// <summary>
        /// Verify a discv5 id-signature: 64-byte (r||s) signature over the
        /// id-signature input hash, signed with the peer's static secp256k1 key.
        /// </summary>
        public static bool VerifyIdSignature(byte[] sigRs, byte[] inputHash, byte[] signerPubKey)
        {
            if (sigRs == null || sigRs.Length != 64) return false;
            if (signerPubKey == null) return false;
            var r = new BigInteger(1, sigRs, 0, 32);
            var s = new BigInteger(1, sigRs, 32, 32);

            byte[] xy;
            try
            {
                if (signerPubKey.Length == 64) xy = signerPubKey;
                else if (signerPubKey.Length == 33) xy = DecompressToXy(signerPubKey);
                else if (signerPubKey.Length == 65 && signerPubKey[0] == 0x04)
                {
                    xy = new byte[64];
                    Buffer.BlockCopy(signerPubKey, 1, xy, 0, 64);
                }
                else return false;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }

            try
            {
                var prefixed = new byte[65];
                prefixed[0] = 0x04;
                Buffer.BlockCopy(xy, 0, prefixed, 1, 64);
                var q = ECKey.Secp256k1.Curve.DecodePoint(prefixed);
                // Public-key validation per NIST SP 800-56A §5.6.2.3.3 — reject
                // identity and off-curve points before accepting a signature
                // recovered against them.
                if (q == null || q.IsInfinity || !q.IsValid()) return false;
                var pubParams = new ECPublicKeyParameters("EC", q, ECKey.CURVE);

                var signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner();
                signer.Init(false, pubParams);
                return signer.VerifySignature(inputHash, r, s);
            }
            // BouncyCastle throws ArgumentException on malformed-curve input,
            // InvalidOperationException on signer-state issues. Anything else
            // (OOM, ThreadAbort) propagates.
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static byte[] LeftPad(byte[] bytes, int length)
        {
            if (bytes.Length == length) return bytes;
            if (bytes.Length > length)
            {
                // BC returns one extra leading 0x00 sometimes when the high byte is set.
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
