using System;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Decoders
{
    public class IntTypeDecoder : TypeDecoder
    {
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

            if (type == typeof(uint))
                return DecodeUInt(encoded);

            if (type == typeof(long))
                return DecodeLong(encoded);

            if (type == typeof(ulong))
                return DecodeULong(encoded);

            if ((type == typeof(BigInteger)) || (type == typeof(object)))
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
            var paddedPrefix = true;

            var negative = encoded.First() == 0xFF;

            if (BitConverter.IsLittleEndian)
                encoded = encoded.Reverse().ToArray();

            if (negative)
                return new BigInteger(encoded) -
                       new BigInteger(
                           "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".HexToByteArray()) - 1;

            return new BigInteger(encoded);
        }

        public byte DecodeByte(byte[] encoded)
        {
            return (byte)DecodeBigInteger(encoded);
        }

        public sbyte DecodeSbyte(byte[] encoded)
        {
            return (sbyte)DecodeBigInteger(encoded);
        }

        public short DecodeShort(byte[] encoded)
        {
            return (short)DecodeBigInteger(encoded);
        }

        public ushort DecodeUShort(byte[] encoded)
        {
            return (ushort)DecodeBigInteger(encoded);
        }

        public int DecodeInt(byte[] encoded)
        {
            return (int) DecodeBigInteger(encoded);
        }

        public long DecodeLong(byte[] encoded)
        {
            return (long) DecodeBigInteger(encoded);
        }

        public uint DecodeUInt(byte[] encoded)
        {
            return (uint) DecodeBigInteger(encoded);
        }

        public ulong DecodeULong(byte[] encoded)
        {
            return (ulong) DecodeBigInteger(encoded);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(BigInteger);
        }

        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(int)) || (type == typeof(uint)) || 
                   (type == typeof(ulong)) || (type == typeof(long))  ||
                   (type == typeof(short)) || (type == typeof(ushort)) ||
                   (type == typeof(byte)) || (type == typeof(sbyte)) ||
                   (type == typeof(BigInteger)) || (type == typeof(object)) ;
        }
    }
}