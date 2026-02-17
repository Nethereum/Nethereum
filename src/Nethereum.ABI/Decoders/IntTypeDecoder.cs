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
            if (type == typeof(byte))
                return DecodeByte(encoded);

            if (type == typeof(sbyte))
                return DecodeSbyte(encoded);

            if (type == typeof(short))
                return DecodeShort(encoded);

            if (type == typeof(ushort))
                return DecodeUShort(encoded);

            if (type == typeof(int))
                return DecodeInt(encoded);

            if (type.GetTypeInfo().IsEnum)
            {
                var val = DecodeInt(encoded);
                return Enum.ToObject(type, val);
            }

            if (type == typeof(uint))
                return DecodeUInt(encoded);

            if (type == typeof(long))
                return DecodeLong(encoded);

            if (type == typeof(ulong))
                return DecodeULong(encoded);

#if NET7_0_OR_GREATER
            if (type == typeof(UInt128))
                return DecodeUInt128(encoded);

            if (type == typeof(Int128))
                return DecodeInt128(encoded);
#endif
            if (type == typeof(BigInteger) || type == typeof(object))
                return DecodeBigInteger(encoded);

            throw new NotSupportedException(type + " is not a supported decoding type for IntType");
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