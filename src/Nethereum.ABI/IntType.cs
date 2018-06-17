using System.Numerics;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class IntType : ABIType
    {
        public static BigInteger MAX_INT256_VALUE = BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819967");
        public static BigInteger MIN_INT256_VALUE = BigInteger.Parse("-57896044618658097711785492504343953926634992332820282019728792003956564819968");
        public static BigInteger MAX_UINT256_VALUE = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
        public static BigInteger MIN_UINT_VALUE = 0;

        public IntType(string name) : base(name)
        {
            Decoder = new IntTypeDecoder(IsSigned(name));
            Encoder = new IntTypeEncoder(IsSigned(name));
        }

        private static bool IsSigned(string name)
        {
            return !name.ToLower().StartsWith("u");
        }

        public override string CanonicalName
        {
            get
            {
                if (Name.Equals("int"))
                    return "int256";
                if (Name.Equals("uint"))
                    return "uint256";
                return base.CanonicalName;
            }
        }
    }
}