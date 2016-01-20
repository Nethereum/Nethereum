using System;

namespace Ethereum.RPC.ABI
{
    public class BoolTypeDecoder:TypeDecoder
    {
        private IntTypeDecoder intTypeDecoder;

        public BoolTypeDecoder()
        {
            intTypeDecoder = new IntTypeDecoder();    
        }
        public bool Decode(byte[] encoded)
        {
            return Decode<bool>(encoded);
        }

        public override bool IsSupportedType(Type type)
        {
            return type == typeof (bool);
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            var decoded = intTypeDecoder.DecodeInt(encoded);
            return Convert.ToBoolean(decoded);
        }
    }
}