using System;

namespace Nethereum.ABI.Util
{
    public class ByteUtil
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

        /// <param name="arrays"> - arrays to merge </param>
        /// <returns> - merged array </returns>
        public static byte[] Merge(params byte[][] arrays)
        {
            var arrCount = 0;
            var count = 0;
            foreach (var array in arrays)
            {
                arrCount++;
                count += array.Length;
            }

            // Create new array and copy all array contents
            var mergedArray = new byte[count];
            var start = 0;
            foreach (var array in arrays)
            {
                Array.Copy(array, 0, mergedArray, start, array.Length);
                start += array.Length;
            }
            return mergedArray;
        }
    }
}