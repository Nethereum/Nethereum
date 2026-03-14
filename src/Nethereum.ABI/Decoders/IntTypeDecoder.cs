using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Nethereum.Hex.HexConvertors.Extensions;

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
using System.Buffers.Binary;
#endif

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

            byte expectedByteValue;
            if (_signed && encoded[0] >= 0x80)
                expectedByteValue = 0xFF;
            else
                expectedByteValue = 0x00;

            if (type == typeof(byte))
            {
                result = DecodeByte(encoded);
                size = 1;
            }
            else if (type == typeof(sbyte))
            {
                var value = DecodeSbyte(encoded);
                size = 1;
                if ((expectedByteValue == 0xFF && value >= 0)
                    || (expectedByteValue == 0x00 && value < 0))
                    throw new OverflowException();
                result = value;
            }
            else if (type == typeof(short))
            {
                var value = DecodeShort(encoded);
                size = 2;
                if ((expectedByteValue == 0xFF && value >= 0)
                    || (expectedByteValue == 0x00 && value < 0))
                    throw new OverflowException();
                result = value;
            }
            else if (type == typeof(ushort))
            {
                result = DecodeUShort(encoded);
                size = 2;
            }
            else if (type == typeof(int))
            {
                var value = DecodeInt(encoded);
                size = 4;
                if ((expectedByteValue == 0xFF && value >= 0)
                    || (expectedByteValue == 0x00 && value < 0))
                    throw new OverflowException();
                result = value;
            }
            else if (type.GetTypeInfo().IsEnum)
            {
                var val = DecodeInt(encoded);
                return Enum.ToObject(type, val);
            }
            else if (type == typeof(uint))
            {
                result = DecodeUInt(encoded);
                size = 4;
            }
            else if (type == typeof(long))
            {
                var value = DecodeLong(encoded);
                size = 8;
                if ((expectedByteValue == 0xFF && value >= 0)
                    || (expectedByteValue == 0x00 && value < 0))
                    throw new OverflowException();
                result = value;
            }
            else if (type == typeof(ulong))
            {
                result = DecodeULong(encoded);
                size = 8;
            }
#if NET7_0_OR_GREATER
            else if (type == typeof(UInt128))
            {
                result = DecodeUInt128(encoded);
                size = 16;
            }
            else if (type == typeof(Int128))
            {
                var value = DecodeInt128(encoded);
                size = 16;
                if ((expectedByteValue == 0xFF && value >= 0)
                    || (expectedByteValue == 0x00 && value < 0))
                    throw new OverflowException();
                result = value;
            }
#endif
            else if (type == typeof(BigInteger) || type == typeof(object))
            {
                return DecodeBigInteger(encoded);
            }

            for (int i = 0; i < encoded.Length - size; i++)
            {
                if (encoded[i] != expectedByteValue)
                    throw new OverflowException();
            }

            if (result is null)
                throw new NotSupportedException(type + " is not a supported decoding type for IntType");

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
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 2)
            {
                var result = BinaryPrimitives.ReadInt16BigEndian(encoded[^2..]);
                ValidateUpperBytes(encoded, 2, _signed && result < 0);
                return result;
            }
#endif
            return (short) DecodeBigInteger(encoded);
        }

        public ushort DecodeUShort(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 2)
            {
                ValidateUpperBytes(encoded, 2, false);
                return BinaryPrimitives.ReadUInt16BigEndian(encoded[^2..]);
            }
#endif
            return (ushort) DecodeBigInteger(encoded);
        }

        public int DecodeInt(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 4)
            {
                var result = BinaryPrimitives.ReadInt32BigEndian(encoded[^4..]);
                ValidateUpperBytes(encoded, 4, _signed && result < 0);
                return result;
            }
#endif
            return (int) DecodeBigInteger(encoded);
        }

        public long DecodeLong(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 8)
            {
                var result = BinaryPrimitives.ReadInt64BigEndian(encoded[^8..]);
                ValidateUpperBytes(encoded, 8, _signed && result < 0);
                return result;
            }
#endif
            return (long) DecodeBigInteger(encoded);
        }

        public uint DecodeUInt(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 4)
            {
                ValidateUpperBytes(encoded, 4, false);
                return BinaryPrimitives.ReadUInt32BigEndian(encoded[^4..]);
            }
#endif
            return (uint) DecodeBigInteger(encoded);
        }

        public ulong DecodeULong(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            if (encoded.Length >= 8)
            {
                ValidateUpperBytes(encoded, 8, false);
                return BinaryPrimitives.ReadUInt64BigEndian(encoded[^8..]);
            }
#endif
            return (ulong) DecodeBigInteger(encoded);
        }

#if NET7_0_OR_GREATER
        public UInt128 DecodeUInt128(byte[] encoded)
        {
            if (encoded.Length >= 16)
            {
                ValidateUpperBytes(encoded, 16, false);
                return BinaryPrimitives.ReadUInt128BigEndian(encoded[^16..]);
            }
            return (UInt128) DecodeBigInteger(encoded);
        }

        public Int128 DecodeInt128(byte[] encoded)
        {
            if (encoded.Length >= 16)
            {
                var result = BinaryPrimitives.ReadInt128BigEndian(encoded[^16..]);
                ValidateUpperBytes(encoded, 16, _signed && result < 0);
                return result;
            }
            return (Int128) DecodeBigInteger(encoded);
        }
#endif
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        private static void ValidateUpperBytes(byte[] encoded, int valueSize, bool negative)
        {
            byte expected = negative ? (byte)0xFF : (byte)0x00;
            for (int i = 0; i < encoded.Length - valueSize; i++)
            {
                if (encoded[i] != expected)
                    throw new OverflowException();
            }
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
    }
}