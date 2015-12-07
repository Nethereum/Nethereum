using System;

namespace Ethereum.ABI.Tests.DNX
{
    public class ByteUtil
    {

        public static readonly sbyte[] EMPTY_BYTE_ARRAY = new sbyte[0];
        public static readonly sbyte[] ZERO_BYTE_ARRAY = new sbyte[] { 0 };

        /// <summary>
        /// Creates a copy of bytes and appends b to the end of it
        /// </summary>
        public static byte[] AppendByte(byte[] bytes, byte b)
        {
            byte[] result = new byte[bytes.Length + 1];
            Array.Copy(bytes, result, bytes.Length);
            result[result.Length - 1] = b;
            return result;
        }


        /// <param name ="arrays"> - arrays to merge </param>
        /// <returns> - merged array </returns>
        public static byte[] Merge(params byte[][] arrays)
        {
            int arrCount = 0;
            int count = 0;
            foreach (byte[] array in arrays)
            {
                arrCount++;
                count += array.Length;
            }

            // Create new array and copy all array contents
            byte[] mergedArray = new byte[count];
            int start = 0;
            foreach (byte[] array in arrays)
            {
                Array.Copy(array, 0, mergedArray, start, array.Length);
                start += array.Length;
            }
            return mergedArray;
        }
    }
}