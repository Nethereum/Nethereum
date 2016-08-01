using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Crypto;
using NBitcoin.BouncyCastle;
using NBitcoin.BouncyCastle.Math;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Core.Signing.Crypto
{
    public class EthECDSASignatureFactory
    {
        public static ECDSASignature FromComponents(byte[] r, byte[] s)
        {
            return new ECDSASignature(new BigInteger(1, r), new BigInteger(1, s));
        }

        public static ECDSASignature FromComponents(byte[] r, byte[] s, byte v)
        {
            ECDSASignature signature = FromComponents(r, s);
            signature.V = v;
            return signature;
        }
    }

    public static class EthECKey
    { 
        public static ECDSASignature SignAndCalculateV(this ECKey key, byte[] hash)
        {
            var signature = key.Sign(hash);
            var recId = key.CalculateRecId(signature, hash);
            signature.V = (byte)(recId + 27);
            return signature;
        }

        public static ECKey RecoverFromSignature(ECDSASignature signature, byte[] hash)
        {
            return ECKey.RecoverFromSignature(GetRecIdFromV(signature.V), signature, hash, false);
        }


        public static int GetRecIdFromV(byte v)
        {
            var header = v;
            // The header byte: 0x1B = first key with even y, 0x1C = first key with odd y,
            //                  0x1D = second key with even y, 0x1E = second key with odd y
            if (header < 27 || header > 34)
            {
                throw new Exception("Header byte out of range: " + header);
            }
            if (header >= 31)
            {
                header -= 4;
            }
            return header - 27;
        }

        public static byte[] GetPubKeyNoPrefix(this ECKey key)
        {
            var pubKey = key.GetPubKey(false);
            var arr = new byte[pubKey.Length - 1];
            //remove the prefix
            Array.Copy(pubKey, 1, arr, 0, arr.Length);
            return arr;

        }

        public static bool VerifyAllowingOnlyLowS(this ECKey key, byte[] hash, ECDSASignature sig)
        {
            if (!sig.IsLowS) return false;
            return key.Verify(hash, sig);
        }

        public static string GetPublicAddress(this ECKey key)
        {
            var initaddr = new Nethereum.ABI.Util.Sha3Keccack().CalculateHash(key.GetPubKeyNoPrefix());
            var addr = new byte[initaddr.Length - 12];
            Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
            return addr.ToHex();
        }

        public static string GetPublicAddress(string privateKey)
        {
            var key = new ECKey(privateKey.HexToByteArray(), true);
            return key.GetPublicAddress();
        }

        public static int CalculateRecId(this ECKey key, ECDSASignature signature, byte[] hash)
        {
            var recId = -1;
            var thisKey = key.GetPubKey(false); // compressed

            for (var i = 0; i < 4; i++)
            {
                var k = ECKey.RecoverFromSignature(i, signature, hash, false).GetPubKey(false);
                if (k != null && Enumerable.SequenceEqual(k, thisKey))
                {
                    recId = i;
                    break;
                }
            }
            if (recId == -1)
            {
                throw new Exception("Could not construct a recoverable key. This should never happen.");
            }
            return recId;
        }
    }
}
