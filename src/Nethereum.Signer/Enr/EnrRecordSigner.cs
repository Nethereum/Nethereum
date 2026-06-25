using System;
using System.Text;
using Nethereum.Model.Enr;
using Nethereum.Signer.Crypto;
using Nethereum.Util;

namespace Nethereum.Signer.Enr
{
    /// <summary>
    /// Signing and verification helpers for EIP-778 ENR records using the v4
    /// identity scheme (secp256k1 over keccak256(rlp([seq, k0, v0, ...]))).
    /// The signature stored on the record is r||s only — no recovery byte.
    /// </summary>
    public static class EnrRecordSigner
    {
        public static void Sign(EnrRecord record, EthECKey privateKey)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));

            if (!record.Pairs.ContainsKey("id"))
                record.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            if (!record.Pairs.ContainsKey("secp256k1"))
                record.Pairs["secp256k1"] = privateKey.GetPubKey(true);

            var content = EnrRecordEncoder.BuildSignedContent(record);
            var digest = new Sha3Keccack().CalculateHash(content);
            var sig = privateKey.SignAndCalculateV(digest);

            var sigBytes = new byte[64];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, sigBytes, 0, 32);
            Buffer.BlockCopy(sig.S.PadBytes(32), 0, sigBytes, 32, 32);
            record.Signature = sigBytes;
        }

        public static bool Verify(EnrRecord record)
        {
            if (record == null) return false;
            if (record.Signature == null || record.Signature.Length != 64) return false;
            if (record.Id != "v4") return false;
            var pubCompressed = record.Secp256k1;
            if (pubCompressed == null || pubCompressed.Length != 33) return false;

            var content = EnrRecordEncoder.BuildSignedContent(record);
            var digest = new Sha3Keccack().CalculateHash(content);

            byte[] r = new byte[32];
            byte[] s = new byte[32];
            Buffer.BlockCopy(record.Signature, 0, r, 0, 32);
            Buffer.BlockCopy(record.Signature, 32, s, 0, 32);

            // ENR signature omits the recovery byte. Try both v=27 and v=28 and accept
            // whichever recovers the matching public key.
            foreach (byte v in new byte[] { 27, 28 })
            {
                try
                {
                    var sig = EthECDSASignatureFactory.FromComponents(r, s, new[] { v });
                    var recovered = EthECKey.RecoverFromSignature(sig, digest);
                    var recoveredCompressed = recovered.GetPubKey(true);
                    if (ByteUtil.AreEqual(recoveredCompressed, pubCompressed))
                        return true;
                }
                catch { }
            }
            return false;
        }
    }
}
