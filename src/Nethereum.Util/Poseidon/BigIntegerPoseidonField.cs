using System;
using System.Numerics;

namespace Nethereum.Util.Poseidon
{
    public class BigIntegerPoseidonField : IPoseidonFieldOps<BigInteger>
    {
        private readonly BigInteger _prime;

        public BigIntegerPoseidonField(BigInteger prime)
        {
            _prime = prime;
        }

        public BigInteger Zero => BigInteger.Zero;

        public BigInteger AddMod(BigInteger a, BigInteger b)
        {
            return Normalize(a + b);
        }

        public BigInteger MulMod(BigInteger a, BigInteger b)
        {
            return Normalize(a * b);
        }

        public BigInteger ModPow(BigInteger baseVal, BigInteger exponent)
        {
            return BigInteger.ModPow(Normalize(baseVal), exponent, _prime);
        }

        public BigInteger FromBytes(byte[] bigEndianData)
        {
            if (bigEndianData == null || bigEndianData.Length == 0)
                return BigInteger.Zero;
            return Normalize(bigEndianData.ToBigIntegerFromUnsignedBigEndian());
        }

        public byte[] ToBytes(BigInteger value)
        {
            var bigEndian = Normalize(value).ToByteArrayUnsignedBigEndian();
            if (bigEndian.Length >= 32)
                return bigEndian;
            var padded = new byte[32];
            Array.Copy(bigEndian, 0, padded, 32 - bigEndian.Length, bigEndian.Length);
            return padded;
        }

        private BigInteger Normalize(BigInteger value)
        {
            var result = value % _prime;
            if (result.Sign < 0)
                result += _prime;
            return result;
        }
    }
}
