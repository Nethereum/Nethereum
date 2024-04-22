using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.ABI.Decoders
{
    public class AddressTypeDecoder : TypeDecoder
    {
        private IntTypeDecoder _intTypeDecoder;

        public AddressTypeDecoder()
        {
            _intTypeDecoder = new IntTypeDecoder();
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            var output = new byte[20];
            Array.Copy(encoded, 12, output, 0, 20);
            return output.ToHex(true).ConvertToEthereumChecksumAddress();
        }

        public override object DecodePacked(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            if(encoded.Length != 20) throw new Exception("Invalid address (should be 20 bytes length): " + encoded.ToHex());
            return encoded.ToHex(true).ConvertToEthereumChecksumAddress();
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(string);
        }

        public override bool IsSupportedType(Type type)
        {
            return type == typeof(string) || type == typeof(object);
        }
    }
}