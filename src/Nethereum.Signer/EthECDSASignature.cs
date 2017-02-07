using System;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;

namespace Nethereum.Signer
{
    public class EthECDSASignature
    {
        private readonly ECDSASignature _ecdsaSignature;
        internal ECDSASignature ECDSASignature => _ecdsaSignature;

        internal EthECDSASignature(BigInteger r, BigInteger s)
        {
            _ecdsaSignature = new ECDSASignature(r, s);
        }

        internal EthECDSASignature(BigInteger r, BigInteger s, byte v)
        {
            _ecdsaSignature = new ECDSASignature(r, s);
            _ecdsaSignature.V = v;
        }

        internal EthECDSASignature(ECDSASignature signature)
        {
            _ecdsaSignature = signature;
        }

        internal EthECDSASignature(BigInteger[] rs)
        {
            _ecdsaSignature = new ECDSASignature(rs);
        }

        public EthECDSASignature(byte[] derSig)
        {
            _ecdsaSignature = new ECDSASignature(derSig);
        }

        public byte[] R => _ecdsaSignature.R.ToByteArrayUnsigned();

        public byte[] S => _ecdsaSignature.S.ToByteArrayUnsigned();


        public byte V { get { return _ecdsaSignature.V; } set { _ecdsaSignature.V = value; } }

        public bool IsLowS => _ecdsaSignature.IsLowS;
      
        public static EthECDSASignature FromDER(byte[] sig)
        {
            return new EthECDSASignature(sig);
        }

        public static bool IsValidDER(byte[] bytes)
        {
            try
            {
                FromDER(bytes);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (Exception ex)
            {
                //	Utils.error("Unexpected exception in ECDSASignature.IsValidDER " + ex.Message);
                return false;
            }
        }
    }
}