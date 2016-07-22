using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Decoders
{
    public class AddressTypeDecoder : TypeDecoder
    {
        private IntTypeDecoder intTypeDecoder;

        public AddressTypeDecoder()
        {
            this.intTypeDecoder = new IntTypeDecoder();

        }
        public override bool IsSupportedType(Type type)
        {
            return type == typeof (string) || type == typeof (object);
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            return intTypeDecoder.DecodeBigInteger(encoded).ToHex(false);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(string);
        }
    }
}