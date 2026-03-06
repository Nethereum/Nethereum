using System;
using System.Text;

namespace Nethereum.ABI.Decoders
{
    public class StringTypeDecoder : TypeDecoder
    {
        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            if (encoded.Length <= 32) return string.Empty;
            var numberOfBytes = EncoderDecoderHelpers.GetNumberOfBytes(encoded);
            if (numberOfBytes == 0) return string.Empty;
            numberOfBytes = Math.Min(numberOfBytes, encoded.Length - 32);
            return Encoding.UTF8.GetString(encoded, 32, numberOfBytes);
        }

        public override object DecodePacked(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            return Encoding.UTF8.GetString(encoded, 0, encoded.Length);
        }

        public string Decode(byte[] encoded)
        {
            return Decode<string>(encoded);
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