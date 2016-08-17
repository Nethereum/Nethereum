using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Decoders
{
    public class AddressTypeDecoder : TypeDecoder
    {
        private IntTypeDecoder intTypeDecoder;

        public AddressTypeDecoder()
        {
            intTypeDecoder = new IntTypeDecoder();
        }

        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(string)) || (type == typeof(object));
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            var output = new byte[20];
            Array.Copy(encoded, 12, output, 0, 20);
            return output.ToHex(true);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(string);
        }
    }
}