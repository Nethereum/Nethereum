#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
using System;
using System.Numerics;

namespace Nethereum.EVM.Precompiles.Backends.ModExp
{
    internal static class ModExpLimbs
    {
        public static ulong[] FromBigEndian(byte[] bytes)
        {
            int length = bytes.Length;
            while (length > 0 && bytes[bytes.Length - length] == 0) length--;
            var limbs = new ulong[(length + 7) / 8];
            for (int i = 0; i < length; i++)
                limbs[i >> 3] |= (ulong)bytes[bytes.Length - 1 - i] << (8 * (i & 7));
            return limbs;
        }

        public static byte[] ToBigEndian(ulong[] limbs, int width)
        {
            var bytes = new byte[width];
            for (int i = 0; i < width && (i >> 3) < limbs.Length; i++)
                bytes[width - 1 - i] = (byte)(limbs[i >> 3] >> (8 * (i & 7)));
            return bytes;
        }

        public static BigInteger ToBigInteger(ulong[] limbs)
        {
            return new BigInteger(ToBigEndian(limbs, limbs.Length * 8), isUnsigned: true, isBigEndian: true);
        }

        public static ulong[] FromBigInteger(BigInteger value)
        {
            return FromBigEndian(value.ToByteArray(isUnsigned: true, isBigEndian: true));
        }

        public static bool IsOne(ulong[] limbs)
        {
            return limbs.Length == 1 && limbs[0] == 1;
        }

        public static int TrailingZeroBits(ulong[] limbs)
        {
            int index = 0;
            while (limbs[index] == 0) index++;
            return index * 64 + BitOperations.TrailingZeroCount(limbs[index]);
        }

        public static ulong[] ShiftRight(ulong[] limbs, int bits)
        {
            int limbShift = bits >> 6;
            int bitShift = bits & 63;
            var shifted = new ulong[limbs.Length - limbShift];
            for (int i = 0; i < shifted.Length; i++)
            {
                ulong low = limbs[i + limbShift] >> bitShift;
                ulong high = bitShift != 0 && i + limbShift + 1 < limbs.Length
                    ? limbs[i + limbShift + 1] << (64 - bitShift)
                    : 0;
                shifted[i] = low | high;
            }
            return Trim(shifted);
        }

        public static ulong[] Trim(ulong[] limbs)
        {
            int length = limbs.Length;
            while (length > 0 && limbs[length - 1] == 0) length--;
            if (length == limbs.Length) return limbs;
            var trimmed = new ulong[length];
            Array.Copy(limbs, trimmed, length);
            return trimmed;
        }

        public static ulong[] Resize(ulong[] limbs, int length)
        {
            var resized = new ulong[length];
            Array.Copy(limbs, resized, Math.Min(length, limbs.Length));
            return resized;
        }

        public static int Compare(ulong[] a, ulong[] b, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            }
            return 0;
        }
    }
}

#endif
