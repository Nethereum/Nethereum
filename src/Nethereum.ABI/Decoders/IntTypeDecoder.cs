using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Nethereum.Hex.HexConvertors.Extensions;


namespace Nethereum.ABI.Decoders
{
    public class IntTypeDecoder : TypeDecoder
    {
        private readonly bool _signed;

        public IntTypeDecoder(bool signed)
        {
            _signed = signed;
        }

        public IntTypeDecoder() : this(false)
        {
        }

        public override object Decode(byte[] encoded, Type type)
        {
            object result = null;
            int size = 32;

            Byte expectedByteValue;
            if (_signed && encoded.First() > 0) // Check that number is negative
                expectedByteValue = 0xFF; // Expected leadings bytes for negative numbers
            else
                expectedByteValue = 0x00; // Expected leading bytes for positive numbers


            if (type == typeof(byte))
            {
                result = DecodeByte(encoded);
                size = sizeof(byte);
            }

            if (type == typeof(sbyte))
            {
                var value = DecodeSbyte(encoded);
                size = sizeof(sbyte);
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(short))
            {
                var value = DecodeShort(encoded);
                size = sizeof(short);
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(ushort))
            {
                result = DecodeUShort(encoded);
                size = sizeof(ushort);
            }

            if (type == typeof(int))
            {
                var value = DecodeInt(encoded);
                size = sizeof(int);
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type.GetTypeInfo().IsEnum)
            {
                var val = DecodeInt(encoded);
                return Enum.ToObject(type, val);
            }

            if (type == typeof(uint))
            {
                result = DecodeUInt(encoded);
                size = sizeof(uint);
            }

            if (type == typeof(long))
            {
                var value = DecodeLong(encoded);
                size = sizeof(long);
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(ulong))
            {
                result = DecodeULong(encoded);
                size = sizeof(ulong);
            }

#if NET7_0_OR_GREATER
            if (type == typeof(UInt128))
            {
                result = DecodeUInt128(encoded);
                size = 16;
            }

            if (type == typeof(Int128))
            {
                var value = DecodeInt128(encoded);
                size = 16;
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }
#endif
            if (type == typeof(BigInteger) || type == typeof(object))
                return DecodeBigInteger(encoded);

            for (int i = 0; i < encoded.Length - size; i++)
            {
                if (encoded[i] != expectedByteValue)
                    throw new OverflowException();
            }

            if (result is null)
                throw new NotSupportedException(type + " is not a supported decoding type for IntType");
            else
                return result;
        }

        public BigInteger DecodeBigInteger(string hexString)
        {
            if (!hexString.StartsWith("0x"))
                hexString = "0x" + hexString;

            return DecodeBigInteger(hexString.HexToByteArray());
        }

        public BigInteger DecodeBigInteger(byte[] encoded)
        {
            var negative = false;
            if (_signed) negative = encoded.First() == 0xFF;

            if (!_signed)
            {
                var listEncoded = encoded.ToList();
                listEncoded.Insert(0, 0x00);
                encoded = listEncoded.ToArray();
            }

            if (BitConverter.IsLittleEndian)
                encoded = encoded.Reverse().ToArray();

            if (negative)
                return new BigInteger(encoded) -
                       new BigInteger(
                           "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".HexToByteArray()) - 1;

            return new BigInteger(encoded);
        }

        public byte DecodeByte(byte[] encoded) => encoded.Last();

        public sbyte DecodeSbyte(byte[] encoded) => (sbyte) encoded.Last();

        public short DecodeShort(byte[] encoded)
        {
            var result = GetIntFromBytesBE(encoded, encoded.Length - 4);
            return (short) result;
        }

        public ushort DecodeUShort(byte[] encoded)
        {
            var result = GetIntFromBytesBE(encoded, encoded.Length - 4);
            return (ushort) result;
        }

        public int DecodeInt(byte[] encoded)
        {
            return GetIntFromBytesBE(encoded, encoded.Length - 4);
        }

        public long DecodeLong(byte[] encoded)
        {
            return (long) DecodeULong(encoded);
        }

        public uint DecodeUInt(byte[] encoded)
        {
            return (uint) GetIntFromBytesBE(encoded, encoded.Length - 4);
        }

        public ulong DecodeULong(byte[] encoded)
        {
            uint high = (uint)GetIntFromBytesBE(encoded, encoded.Length - 8);
            uint low = (uint)GetIntFromBytesBE(encoded, encoded.Length - 4);
            ulong result = ((ulong)high << 32) | low;
            return result;
        }

#if NET7_0_OR_GREATER
        public UInt128 DecodeUInt128(byte[] encoded)
        {
            UInt128 low = (UInt128) DecodeULong(encoded);
            uint highHigh = (uint) GetIntFromBytesBE(encoded, encoded.Length - 16);
            uint highLow = (uint) GetIntFromBytesBE(encoded, encoded.Length - 12);
            UInt128 highPart = ((UInt128) highHigh << (64 + 32)) | ((UInt128) highLow << 64);
            return highPart | low;
        }

        public Int128 DecodeInt128(byte[] encoded)
        {
            return (Int128) DecodeUInt128(encoded);
        }
#endif
        public override Type GetDefaultDecodingType()
        {
            return typeof(BigInteger);
        }

        public override bool IsSupportedType(Type type)
        {
            return type == typeof(int) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(long) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(BigInteger) || type == typeof(object) ||
#if NET7_0_OR_GREATER
                   type == typeof(UInt128) || type == typeof(Int128) ||
#endif
                   type.GetTypeInfo().IsEnum;
        }

        public override object DecodePacked(byte[] encoded, Type type)
        { 
           return Decode(encoded, type);
        }

        /// <summary>
        /// Converts four consecutive bytes from the specified array, starting at the given index, into a 32-bit signed
        /// integer using big-endian byte order.
        /// </summary>
        /// <remarks>The method does not perform bounds checking. If there are fewer than four bytes
        /// available from the specified index, an exception may be thrown.</remarks>
        /// <param name="encoded">The byte array containing the bytes to convert.</param>
        /// <param name="startIndex">The zero-based index in the array at which to begin reading the four bytes. Must be less than or equal to
        /// the length of the array minus four.</param>
        /// <returns>A 32-bit signed integer representing the value of the four bytes interpreted in big-endian order.</returns>
        internal int GetIntFromBytesBE(byte[] encoded, int startIndex)
        {
            int result = encoded[startIndex] << 24;
            result |= encoded[startIndex + 1] << 16;
            result |= encoded[startIndex + 2] << 8;
            return result | encoded[startIndex + 3];
        }
    }
}