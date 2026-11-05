using System.Numerics;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
using Nethereum.EVM.Precompiles.Backends.ModExp;
#endif

namespace Nethereum.EVM.Precompiles.Backends
{
    public sealed class DefaultModExpBackend : IModExpBackend
    {
        public static readonly DefaultModExpBackend Instance = new DefaultModExpBackend();

#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
        private const int SmallExponentBitLimit = 8;

        public byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus)
        {
            var modulusLimbs = ModExpLimbs.FromBigEndian(modulus);
            var result = modulusLimbs.Length == 0
                ? modulusLimbs
                : Power(baseBytes ?? new byte[0], new ModExpExponent(expBytes ?? new byte[0]), modulusLimbs);
            return ModExpLimbs.ToBigEndian(result, modulus.Length);
        }

        private static ulong[] Power(byte[] baseBytes, ModExpExponent exponent, ulong[] modulus)
        {
            if (ModExpLimbs.IsOne(modulus)) return new ulong[0];
            if (exponent.IsZero) return new ulong[] { 1 };
            if (exponent.BitLength <= SmallExponentBitLimit) return PowerByBigInteger(baseBytes, exponent, modulus);
            int twos = ModExpLimbs.TrailingZeroBits(modulus);
            if (twos == 0) return OddModulusExponentiation.Power(baseBytes, exponent, modulus);
            var oddPart = ModExpLimbs.ShiftRight(modulus, twos);
            var low = PowerOfTwoModulusExponentiation.Power(baseBytes, exponent, twos);
            if (ModExpLimbs.IsOne(oddPart)) return low;
            var high = OddModulusExponentiation.Power(baseBytes, exponent, oddPart);
            return CombineByChineseRemainder(high, low, oddPart, twos);
        }

        private static ulong[] PowerByBigInteger(byte[] baseBytes, ModExpExponent exponent, ulong[] modulus)
        {
            var baseValue = new BigInteger(baseBytes, isUnsigned: true, isBigEndian: true);
            var exponentValue = new BigInteger(exponent.BigEndian, isUnsigned: true, isBigEndian: true);
            return ModExpLimbs.FromBigInteger(BigInteger.ModPow(baseValue, exponentValue, ModExpLimbs.ToBigInteger(modulus)));
        }

        private static ulong[] CombineByChineseRemainder(ulong[] modOdd, ulong[] modPowerOfTwo, ulong[] oddPart, int twos)
        {
            var mask = (BigInteger.One << twos) - 1;
            var q = ModExpLimbs.ToBigInteger(oddPart);
            var high = ModExpLimbs.ToBigInteger(modOdd);
            var low = ModExpLimbs.ToBigInteger(modPowerOfTwo);
            var lift = (((low - high) & mask) * InverseModPowerOfTwo(q, twos)) & mask;
            return ModExpLimbs.FromBigInteger(high + q * lift);
        }

        private static BigInteger InverseModPowerOfTwo(BigInteger odd, int bits)
        {
            var inverse = BigInteger.One;
            for (int precision = 1; precision < bits; precision *= 2)
            {
                var mask = (BigInteger.One << (precision * 2)) - 1;
                inverse = (inverse * (2 - odd * inverse)) & mask;
            }
            return inverse;
        }
#else
        public byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus)
        {
            var modulusValue = FromBigEndian(modulus);
            if (modulusValue.IsZero) return new byte[modulus.Length];
            var result = BigInteger.ModPow(FromBigEndian(baseBytes ?? new byte[0]), FromBigEndian(expBytes ?? new byte[0]), modulusValue);
            return ToBigEndian(result, modulus.Length);
        }

        private static BigInteger FromBigEndian(byte[] bytes)
        {
            var littleEndian = new byte[bytes.Length + 1];
            for (int i = 0; i < bytes.Length; i++) littleEndian[i] = bytes[bytes.Length - 1 - i];
            return new BigInteger(littleEndian);
        }

        private static byte[] ToBigEndian(BigInteger value, int length)
        {
            var littleEndian = value.ToByteArray();
            var result = new byte[length];
            for (int i = 0; i < length && i < littleEndian.Length; i++) result[length - 1 - i] = littleEndian[i];
            return result;
        }
#endif
    }
}
