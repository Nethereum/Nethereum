using System;
using System.Text;

namespace Nethereum.ABI.Decoders
{
    public class StringTypeDecoder : TypeDecoder
    {
        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(string)) || (type == typeof(object));
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            return Encoding.UTF8.GetString(encoded, 32, EncoderDecoderHelpers.GetNumberOfBytes(encoded));
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(string);
        }

        public string Decode(byte[] encoded)
        {
            return Decode<string>(encoded);
        }
    }
}