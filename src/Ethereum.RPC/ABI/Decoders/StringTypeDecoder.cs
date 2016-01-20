using System;

namespace Ethereum.RPC.ABI
{
    public class StringTypeDecoder:TypeDecoder
    {
        public override bool IsSupportedType(Type type)
        {
            return type == typeof (string);
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if(!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            return System.Text.Encoding.UTF8.GetString(encoded, 32, EncoderDecoderHelpers.GetNumberOfBytes(encoded));
        }

        public string Decode(byte[] encoded)
        {
            return Decode<string>(encoded);
        }
    }
}