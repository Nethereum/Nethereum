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

            Byte expectedByteValue;
            if (_signed && encoded.First() > 0) // Check that number is negative
                expectedByteValue = 0xFF; // Expected leadings bytes for negative numbers
            else
                expectedByteValue = 0x00; // Expected leading bytes for positive numbers

            if (type == typeof(byte))
            {
                result = DecodeByte(encoded);
                size = 1;
            }

            if (type == typeof(sbyte))
            {
                var value = DecodeSbyte(encoded);
                size = 1;
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(short))
            {
                var value = DecodeShort(encoded);
                size = 2;
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(ushort))
            {
                result = DecodeUShort(encoded);
                size = 2;
            }

            if (type == typeof(int))
            {
                var value = DecodeInt(encoded);
                size = 4;
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
                size = 4;
            }

            if (type == typeof(long))
            {
                var value = DecodeLong(encoded);
                size = 8;
                if (expectedByteValue == 0xFF && value >= 0
                    || expectedByteValue == 0x00 && value < 0)
                    throw new OverflowException();
                result = value;
            }

            if (type == typeof(ulong))
            {
                result = DecodeULong(encoded);
                size = 8;
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
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadInt16BigEndian(encoded[^2..]);
#else
            return (short) DecodeBigInteger(encoded);
#endif
        }

        public ushort DecodeUShort(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt16BigEndian(encoded[^2..]);
#else
            return (ushort) DecodeBigInteger(encoded);
#endif
        }

        public int DecodeInt(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadInt32BigEndian(encoded[^4..]);
#else
            return (int) DecodeBigInteger(encoded);
#endif
        }

        public long DecodeLong(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadInt64BigEndian(encoded[^8..]);
#else
            return (long) DecodeBigInteger(encoded);
#endif
        }

        public uint DecodeUInt(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt32BigEndian(encoded[^4..]);
#else
            return (uint) DecodeBigInteger(encoded);
#endif
        }

        public ulong DecodeULong(byte[] encoded)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt64BigEndian(encoded[^8..]);
#else
            return (ulong) DecodeBigInteger(encoded);
#endif
        }

#if NET7_0_OR_GREATER
        public UInt128 DecodeUInt128(byte[] encoded)
        {
            return BinaryPrimitives.ReadUInt128BigEndian(encoded[^16..]);
        }

        public Int128 DecodeInt128(byte[] encoded)
        {
            return BinaryPrimitives.ReadInt128BigEndian(encoded[^16..]);
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