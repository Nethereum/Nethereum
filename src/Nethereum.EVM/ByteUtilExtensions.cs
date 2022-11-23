using System;
using System.Numerics;
using Nethereum.ABI;
using System.Collections;

namespace Nethereum.EVM
{
    public static class ByteUtilExtensions
    {

        
        public static byte[] PadTo32Bytes(this byte[] bytesToPad)
        {
            return PadBytes(bytesToPad, 32);
        }

        public static byte[] PadTo128Bytes(this byte[] bytesToPad)
        {
            return PadBytes(bytesToPad, 128);
        }

        public static byte[] PadBytes(this byte[] bytesToPad, int numberOfBytes)
        {
            var ret = new byte[numberOfBytes];

            for (var i = 0; i < ret.Length; i++)
                ret[i] = 0;
            Array.Copy(bytesToPad, 0, ret, numberOfBytes - bytesToPad.Length, bytesToPad.Length);

            return ret;
        }
        //TODO CHECK FRAMEWORK AND USE INTERNAL
        public static byte[] ShiftLeft(this byte[] value, int shift)
        {
//#if NETCOREAPP2_0_OR_GREATER

//            var bitArray = new BitArray(value);
//            var returnByteArray = new byte[value.Length];
//            bitArray.LeftShift(shift);
//            bitArray.CopyTo(returnByteArray, 0);
//            return returnByteArray;
//#else
            byte[] newValue = new byte[value.Length];
            byte overflow = 0x00;

            for (int i = value.Length - 1; i >= 0; i--)
            {
                int byteEndPosition = (i * 8) - shift + 7;
                int resultBytePosition = byteEndPosition / 8;

                if (byteEndPosition >= 0)
                {
                    newValue[resultBytePosition] = (byte)(value[i] << (shift % 8));
                    newValue[resultBytePosition] |= overflow;
                    overflow = (byte)(((value[i] << (shift % 8)) & 0xFF00) >> 8);
                }
            }

            return newValue;
//#endif
        }

        public static byte[] ShiftRight(this byte[] value, int shift)
        {
//#if NETCOREAPP2_0_OR_GREATER

//            var bitArray = new BitArray(value);
//            var returnByteArray = new byte[value.Length];
//            bitArray.RightShift(shift);
//            bitArray.CopyTo(returnByteArray, 0);
//            return returnByteArray;
//#else
            byte[] newValue = new byte[value.Length];
            byte overflow = 0x00;

            for (int i = 0; i < value.Length; i++)
            {
                int byteStartPosition = (i * 8) + shift;
                int resultBytePosition = byteStartPosition / 8;

                if (resultBytePosition < value.Length)
                {
                    newValue[resultBytePosition] = (byte)(value[i] >> (shift % 8));
                    newValue[resultBytePosition] |= overflow;
                    overflow = (byte)((value[i] << (8 - (shift % 8))) & 0xFF);
                }
            }

            

            return newValue;
//#endif
        }

        public static BigInteger ConvertToInt256(this byte[] bytes)
        {
            var value = new IntType("int256").Decode<BigInteger>(bytes);

            if (value > IntType.MAX_INT256_VALUE)
            {
                value = 1 + IntType.MAX_UINT256_VALUE - value;
            }

            return value;
        }

        public static BigInteger ConvertToUInt256(this byte[] bytes)
        {
            return new IntType("uint256").Decode<BigInteger>(bytes);
        }

    }
}