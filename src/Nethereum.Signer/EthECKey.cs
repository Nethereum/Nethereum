using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
using NBitcoin.Secp256k1;
#endif
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace Nethereum.Signer
{

    public class EthECKey
    {
        private static readonly SecureRandom SecureRandom = new SecureRandom();

#if NETCOREAPP3_1 || NET5_0_OR_GREATER
        /// <summary>
        /// Enables / Disables whilst signing creating a recoverable id, as opposed to afterward. When enabled this uses NBitcoin.Secp256k1 as opposed to BouncyCastle to create the signature.
        /// </summary>
        public static bool SignRecoverable { get; set; } = false;
#endif
        public static byte DEFAULT_PREFIX = 0x04;
        private readonly ECKey _ecKey;
        private byte[] _publicKey;
        private byte[] _publicKeyCompressed;
        private byte[] _publicKeyNoPrefix;
        private byte[] _publicKeyNoPrefixCompressed;
        private string _ethereumAddress;
        private byte[] _privateKey;
        private string _privateKeyHex;


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
            _ecKey = new ECKey(ByteUtil.Merge(new[] { prefix }, vch), isPrivate);
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
            var thisKey = _ecKey.GetPubKey(false); // compressed
            return CalculateRecId(signature, hash, thisKey);
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

        public static EthECKey GenerateKey(byte[] seed = null)
        {
            var secureRandom = SecureRandom;
            if (seed != null)
            {
                secureRandom = new SecureRandom();
                secureRandom.SetSeed(seed);
            }

            var gen = new ECKeyPairGenerator("EC");
            var keyGenParam = new KeyGenerationParameters(secureRandom, 256);
            gen.Init(keyGenParam);
            var keyPair = gen.GenerateKeyPair();
            var privateBytes = ((ECPrivateKeyParameters)keyPair.Private).D.ToByteArrayUnsigned();
            if (privateBytes.Length != 32)
                return GenerateKey();
            return new EthECKey(privateBytes, true);
        }

        public static EthECKey GenerateKey()
        {
            var gen = new ECKeyPairGenerator("EC");
            var keyGenParam = new KeyGenerationParameters(SecureRandom, 256);
            gen.Init(keyGenParam);
            var keyPair = gen.GenerateKeyPair();
            var privateBytes = ((ECPrivateKeyParameters)keyPair.Private).D.ToByteArrayUnsigned();
            if (privateBytes.Length != 32)
                return GenerateKey();
            return new EthECKey(privateBytes, true);
        }

        public byte[] GetPrivateKeyAsBytes()
        {
            if (_privateKey == null)
            {
                _privateKey = _ecKey.PrivateKey.D.ToByteArrayUnsigned();
            }
            return _privateKey;
        }

        public string GetPrivateKey()
        {
            if (_privateKeyHex == null)
            {
                _privateKeyHex = GetPrivateKeyAsBytes().ToHex(true);
            }
            return _privateKeyHex;
        }

        public byte[] GetPubKey(bool compresseed = false)
        {
            if (!compresseed)
            {
                if (_publicKey == null)
                {
                    _publicKey = _ecKey.GetPubKey(false);
                }
                return _publicKey;
            }
            else
            {
                if (_publicKeyCompressed == null)
                {
                    _publicKeyCompressed = _ecKey.GetPubKey(true);
                }
                return _publicKeyCompressed;

            }
        }

        public byte[] GetPubKeyNoPrefix(bool compressed = false)
        {
            if (!compressed)
            {
                if (_publicKeyNoPrefix == null)
                {
                    var pubKey = _ecKey.GetPubKey(false);
                    var arr = new byte[pubKey.Length - 1];
                    //remove the prefix
                    Array.Copy(pubKey, 1, arr, 0, arr.Length);
                    _publicKeyNoPrefix = arr;
                }
                return _publicKeyNoPrefix;
            }
            else
            {
                if (_publicKeyNoPrefixCompressed == null)
                {
                    var pubKey = _ecKey.GetPubKey(true);
                    var arr = new byte[pubKey.Length - 1];
                    //remove the prefix
                    Array.Copy(pubKey, 1, arr, 0, arr.Length);
                    _publicKeyNoPrefixCompressed = arr;
                }
                return _publicKeyNoPrefixCompressed;

            }
        }

        public string GetPublicAddress()
        {
            if (_ethereumAddress == null)
            {
                var initaddr = new Sha3Keccack().CalculateHash(GetPubKeyNoPrefix());
                var addr = new byte[initaddr.Length - 12];
                Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
                _ethereumAddress = new AddressUtil().ConvertToChecksumAddress(addr.ToHex());
            }
            return _ethereumAddress;
        }

        public byte[] GetPublicAddressAsBytes()
        {
            if (_ethereumAddress == null)
            {
                var initaddr = new Sha3Keccack().CalculateHash(GetPubKeyNoPrefix());
                var addr = new byte[initaddr.Length - 12];
                Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
                return addr;
            }
            return _ethereumAddress.HexToByteArray();
        }

        public static string GetPublicAddress(string privateKey)
        {
            var key = new EthECKey(privateKey.HexToByteArray(), true);
            return key.GetPublicAddress();
        }

        public static int GetRecIdFromV(byte[] v)
        {
            return VRecoveryAndChainCalculations.GetRecIdFromV(v);
        }


        public static int GetRecIdFromV(byte v)
        {
            return VRecoveryAndChainCalculations.GetRecIdFromV(v);
        }

        public static int GetRecIdFromVChain(BigInteger vChain, BigInteger chainId)
        {
            return VRecoveryAndChainCalculations.GetRecIdFromVChain(vChain, chainId);
        }

        public static BigInteger GetChainFromVChain(BigInteger vChain)
        {
            return VRecoveryAndChainCalculations.GetChainFromVChain(vChain);
        }

        public static int GetRecIdFromVChain(byte[] vChain, BigInteger chainId)
        {
            return VRecoveryAndChainCalculations.GetRecIdFromVChain(vChain, chainId);
            
        }

        public static EthECKey RecoverFromSignature(EthECDSASignature signature, byte[] hash)
        {
            return new EthECKey(ECKey.RecoverFromSignature(GetRecIdFromV(signature.V), signature.ECDSASignature, hash,
                false));
        }

        public static EthECKey RecoverFromParityYSignature(EthECDSASignature signature, byte[] hash)
        {
            return new EthECKey(ECKey.RecoverFromSignature(signature.V.ToIntFromRLPDecoded(), signature.ECDSASignature, hash,
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
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            if (SignRecoverable)
            {
                var privKey = Context.Instance.CreateECPrivKey(GetPrivateKeyAsBytes());
                privKey.TrySignRecoverable(hash, out var recSignature);
                recSignature.Deconstruct(out Scalar r, out var s, out var recId);
                var vChain = CalculateV(chainId, recId);
                return EthECDSASignatureFactory.FromComponents(r.ToBytes(), s.ToBytes(),
                    vChain.ToBytesForRLPEncoding());
            }
            else
            {
#endif
            var signature = _ecKey.Sign(hash);
            var recId = CalculateRecId(signature, hash);
            var vChain = CalculateV(chainId, recId);
            signature.V = vChain.ToBytesForRLPEncoding();
            return new EthECDSASignature(signature);

#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            }
#endif
        }

        public EthECDSASignature SignAndCalculateYParityV(byte[] hash)
        {
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            if (SignRecoverable)
            {
                var privKey = Context.Instance.CreateECPrivKey(GetPrivateKeyAsBytes());
                privKey.TrySignRecoverable(hash, out var recSignature);
                recSignature.Deconstruct(out Scalar r, out var s, out var recId);
                return EthECDSASignatureFactory.FromComponents(r.ToBytes(), s.ToBytes(), new[] { (byte)(recId) });
            }
            else
            {
#endif
            var signature = _ecKey.Sign(hash);
            var recId = CalculateRecId(signature, hash);
            signature.V = new[] { (byte)(recId) };
            return new EthECDSASignature(signature);

#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            }
#endif
        }

        internal static BigInteger CalculateV(BigInteger chainId, int recId)
        {
            return VRecoveryAndChainCalculations.CalculateV(chainId, recId);
        }


        public EthECDSASignature SignAndCalculateV(byte[] hash)
        {
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            if (SignRecoverable)
            {
                var privKey = Context.Instance.CreateECPrivKey(GetPrivateKeyAsBytes());
                privKey.TrySignRecoverable(hash, out var recSignature);
                recSignature.Deconstruct(out Scalar r, out var s, out var recId);
                return EthECDSASignatureFactory.FromComponents(r.ToBytes(), s.ToBytes(), new[] { (byte)(recId + 27) });
            }
            else
            {
#endif
            var signature = _ecKey.Sign(hash);
            var recId = CalculateRecId(signature, hash);
            signature.V = new[] { (byte)(recId + 27) };
            return new EthECDSASignature(signature);
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            }
#endif
        }

        public EthECDSASignature Sign(byte[] hash)
        {
            var signature = _ecKey.Sign(hash);
            return new EthECDSASignature(signature);
        }

        public bool Verify(byte[] hash, EthECDSASignature sig)
        {
            var currentSignature = sig as EthECDSASignature;
            return _ecKey.Verify(hash, currentSignature.ECDSASignature);
        }

        public bool VerifyAllowingOnlyLowS(byte[] hash, EthECDSASignature sig)
        {
            var currentSignature = sig as EthECDSASignature;
            if (!sig.IsLowS) return false;
            return _ecKey.Verify(hash, currentSignature.ECDSASignature);
        }
    }
}