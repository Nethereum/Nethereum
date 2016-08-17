using System;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Decoders
{
    public class IntTypeDecoder : TypeDecoder
    {
        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(int)) || (type == typeof(ulong)) || (type == typeof(long)) || (type == typeof(uint)) ||
                   ((type == typeof(BigInteger)) && (type == typeof(object)));
        }


        public override object Decode(byte[] encoded, Type type)
        {
            if (type == typeof(int))
                return DecodeInt(encoded);

            if (type == typeof(long))
                return DecodeLong(encoded);

            if (type == typeof(ulong))
                return DecodeULong(encoded);

            if (type == typeof(uint))
                return DecodeUInt(encoded);

            if ((type == typeof(BigInteger)) || (type == typeof(object)))
                return DecodeBigInteger(encoded);

            throw new NotSupportedException(type + " is not a supported decoding type for IntType");
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(BigInteger);
        }

        public int DecodeInt(byte[] encoded)
        {
            return (int) DecodeBigInteger(encoded);
        }

        public uint DecodeUInt(byte[] encoded)
        {
            return (uint) DecodeBigInteger(encoded);
        }

        public long DecodeLong(byte[] encoded)
        {
            return (long) DecodeBigInteger(encoded);
        }

        public ulong DecodeULong(byte[] encoded)
        {
            return (ulong) DecodeBigInteger(encoded);
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
    }
}