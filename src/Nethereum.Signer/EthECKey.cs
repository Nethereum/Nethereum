using System;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace Nethereum.Signer
{
    public class EthECKey
    {
        private static readonly SecureRandom SecureRandom = new SecureRandom();
        public static byte DEFAULT_PREFIX = 0x04;
        private readonly ECKey _ecKey;

        public EthECKey(string privateKey)
        {
            _ecKey = new ECKey(privateKey.HexToByteArray(), true);
        }


        public EthECKey(byte[] vch, bool isPrivate)
        {
            _ecKey = new ECKey(vch, isPrivate);
        }

        public EthECKey(byte[] vch, bool isPrivate, byte prefix)
        {
            _ecKey = new ECKey(ByteUtil.Merge(new[] {prefix}, vch), isPrivate);
        }

        internal EthECKey(ECKey ecKey)
        {
            _ecKey = ecKey;
        }


        public byte[] CalculateCommonSecret(EthECKey publicKey)
        {
            var agreement = new ECDHBasicAgreement();
            agreement.Init(_ecKey.PrivateKey);
            var z = agreement.CalculateAgreement(publicKey._ecKey.GetPublicKeyParameters());

            return BigIntegers.AsUnsignedByteArray(agreement.GetFieldSize(), z);
        }

        //Note: Y coordinates can only be forced, so it is assumed 0 and 1 will be the recId (even if implementation allows for 2 and 3)
        internal int CalculateRecId(ECDSASignature signature, byte[] hash)
        {
            //var recId = -1;
            var thisKey = _ecKey.GetPubKey(false); // compressed
            return CalculateRecId(signature, hash, thisKey);
            //for (var i = 0; i < 4; i++)
            //{
            //    var rec = ECKey.RecoverFromSignature(i, signature, hash, false);
            //    if (rec != null)
            //    {
            //        var k = rec.GetPubKey(false);
            //        if (k != null && k.SequenceEqual(thisKey))
            //        {
            //            recId = i;
            //            break;
            //        }
            //    }
            //}
            //if (recId == -1)
            //    throw new Exception("Could not construct a recoverable key. This should never happen.");
            //return recId;
        }

        internal static int CalculateRecId(ECDSASignature signature, byte[] hash, byte[] uncompressedPublicKey)
        {
            var recId = -1;

            for (var i = 0; i < 4; i++)
            {
                var rec = ECKey.RecoverFromSignature(i, signature, hash, false);
                if (rec != null)
                {
                    var k = rec.GetPubKey(false);
                    if (k != null && k.SequenceEqual(uncompressedPublicKey))
                    {
                        recId = i;
                        break;
                    }
                }
            }
            if (recId == -1)
                throw new Exception("Could not construct a recoverable key. This should never happen.");
            return recId;
        }

        public static EthECKey GenerateKey()
        {
            var gen = new ECKeyPairGenerator("EC");
            var keyGenParam = new KeyGenerationParameters(SecureRandom, 256);
            gen.Init(keyGenParam);
            var keyPair = gen.GenerateKeyPair();
            var privateBytes = ((ECPrivateKeyParameters) keyPair.Private).D.ToByteArray();
            if (privateBytes.Length != 32)
                return GenerateKey();
            return new EthECKey(privateBytes, true);
        }

        public byte[] GetPrivateKeyAsBytes()
        {
            return _ecKey.PrivateKey.D.ToByteArray();
        }

        public string GetPrivateKey()
        {
            return GetPrivateKeyAsBytes().ToHex(true);
        }

        public byte[] GetPubKey()
        {
            return _ecKey.GetPubKey(false);
        }

        public byte[] GetPubKeyNoPrefix()
        {
            var pubKey = _ecKey.GetPubKey(false);
            var arr = new byte[pubKey.Length - 1];
            //remove the prefix
            Array.Copy(pubKey, 1, arr, 0, arr.Length);
            return arr;
        }

        public string GetPublicAddress()
        {
            var initaddr = new Sha3Keccack().CalculateHash(GetPubKeyNoPrefix());
            var addr = new byte[initaddr.Length - 12];
            Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
            return new AddressUtil().ConvertToChecksumAddress(addr.ToHex());
        }

        public static string GetPublicAddress(string privateKey)
        {
            var key = new EthECKey(privateKey.HexToByteArray(), true);
            return key.GetPublicAddress();
        }

        public static int GetRecIdFromV(byte[] v)
        {
            return GetRecIdFromV(v[0]);
        }


        public static int GetRecIdFromV(byte v)
        {
            var header = v;
            // The header byte: 0x1B = first key with even y, 0x1C = first key with odd y,
            //                  0x1D = second key with even y, 0x1E = second key with odd y
            if (header < 27 || header > 34)
                throw new Exception("Header byte out of range: " + header);
            if (header >= 31)
                header -= 4;
            return header - 27;
        }

        public static int GetRecIdFromVChain(BigInteger vChain, BigInteger chainId)
        {
            return (int) (vChain - chainId * 2 - 35);
        }

        public static BigInteger GetChainFromVChain(BigInteger vChain)
        {
            var start = vChain - 35;
            var even = start % 2 == 0;
            if (even) return start / 2;
            return (start - 1) / 2;
        }

        public static int GetRecIdFromVChain(byte[] vChain, BigInteger chainId)
        {
            return GetRecIdFromVChain(vChain.ToBigIntegerFromRLPDecoded(), chainId);
        }

        public static EthECKey RecoverFromSignature(EthECDSASignature signature, byte[] hash)
        {
            return new EthECKey(ECKey.RecoverFromSignature(GetRecIdFromV(signature.V), signature.ECDSASignature, hash,
                false));
        }

        public static EthECKey RecoverFromSignature(EthECDSASignature signature, int recId, byte[] hash)
        {
            return new EthECKey(ECKey.RecoverFromSignature(recId, signature.ECDSASignature, hash, false));
        }

        public static EthECKey RecoverFromSignature(EthECDSASignature signature, byte[] hash, BigInteger chainId)
        {
            return new EthECKey(ECKey.RecoverFromSignature(GetRecIdFromVChain(signature.V, chainId),
                signature.ECDSASignature, hash, false));
        }

        public EthECDSASignature SignAndCalculateV(byte[] hash, BigInteger chainId)
        {
            var signature = _ecKey.Sign(hash);
            var recId = CalculateRecId(signature, hash);
            var vChain = CalculateV(chainId, recId);
            signature.V = vChain.ToBytesForRLPEncoding();
            return new EthECDSASignature(signature);
        }

        internal static BigInteger CalculateV(BigInteger chainId, int recId)
        {
            return chainId * 2 + recId + 35;
        }

        public EthECDSASignature SignAndCalculateV(byte[] hash)
        {
            var signature = _ecKey.Sign(hash);
            var recId = CalculateRecId(signature, hash);
            signature.V = new[] {(byte) (recId + 27)};
            return new EthECDSASignature(signature);
        }

        public EthECDSASignature Sign(byte[] hash)
        {
            var signature = _ecKey.Sign(hash);
            return new EthECDSASignature(signature);
        }

        public bool Verify(byte[] hash, EthECDSASignature sig)
        {
            return _ecKey.Verify(hash, sig.ECDSASignature);
        }

        public bool VerifyAllowingOnlyLowS(byte[] hash, EthECDSASignature sig)
        {
            if (!sig.IsLowS) return false;
            return _ecKey.Verify(hash, sig.ECDSASignature);
        }
    }
}