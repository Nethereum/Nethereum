using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Util
{
    /// <summary>
    /// Utility class for bit manipulation operations
    /// </summary>
    public static class BitUtils
    {

        public static byte[] FlipBit(byte[] bytes, int bitPosition)
        {
            var result = new byte[bytes.Length];
            Array.Copy(bytes, result, bytes.Length);

            int byteIndex = bitPosition / 8;
            int bitIndex = bitPosition % 8;

            if (byteIndex < result.Length)
            {
                result[byteIndex] ^= (byte)(1 << bitIndex);
            }

            return result;
        }

        public static byte[] SetBit(byte[] bytes, int bitPosition)
        {
            var result = new byte[bytes.Length];
            Array.Copy(bytes, result, bytes.Length);

            int byteIndex = bitPosition / 8;
            int bitIndex = bitPosition % 8;

            if (byteIndex < result.Length)
            {
                result[byteIndex] |= (byte)(1 << bitIndex);
            }

            return result;
        }

        public static byte[] ClearBit(byte[] data, int bitIndex)
        {
            var result = new byte[data.Length];
            Array.Copy(data, result, data.Length);

            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;

            if (byteIndex < result.Length)
            {
                result[byteIndex] &= (byte)~(1 << bitOffset);
            }

            return result;
        }


        public static bool GetBit(byte[] data, int bitIndex)
        {
            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;

            if (byteIndex >= data.Length)
                return false;

            return (data[byteIndex] & (1 << bitOffset)) != 0;
        }


    }
}
