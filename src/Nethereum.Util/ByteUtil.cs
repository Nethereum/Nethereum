using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Util
{
    public static class ByteUtil
    {
        public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        public static readonly byte[] ZERO_BYTE_ARRAY = {0};

        /// <summary>
        ///     Creates a copy of bytes and appends b to the end of it
        /// </summary>
        public static byte[] AppendByte(byte[] bytes, byte b)
        {
            var result = new byte[bytes.Length + 1];
            Array.Copy(bytes, result, bytes.Length);
            result[result.Length - 1] = b;
            return result;
        }

        public static byte[] Slice(this byte[] org,
            int start, int end = int.MaxValue)
        {
            if (end < 0)
                end = org.Length + end;
            start = Math.Max(0, start);
            end = Math.Max(start, end);

            return org.Skip(start).Take(end - start).ToArray();
        }

        public static byte[] InitialiseEmptyByteArray(int length)
        {
            var returnArray = new byte[length];
            for (var i = 0; i < length; i++)
                returnArray[i] = 0x00;
            return returnArray;
        }

        public static IEnumerable<byte> MergeToEnum(params byte[][] arrays)
        {
            foreach (var a in arrays)
            foreach (var b in a)
                yield return b;
        }

        /// <param name="arrays"> - arrays to merge </param>
        /// <returns> - merged array </returns>
        public static byte[] Merge(params byte[][] arrays)
        {
            return MergeToEnum(arrays).ToArray();
        }

        public static byte[] XOR(this byte[] a, byte[] b)
        {
            var length = Math.Min(a.Length, b.Length);
            var result = new byte[length];
            for (var i = 0; i < length; i++)
                result[i] = (byte) (a[i] ^ b[i]);
            return result;
        }
    }
}