using System;

namespace Nethereum.ABI.Decoders
{
    public class Bytes32TypeDecoder: TypeDecoder
    {
        private IntTypeDecoder intTypeDecoder;
        private BoolTypeDecoder boolTypeDecoder;

        public Bytes32TypeDecoder()
        {
            this.intTypeDecoder = new IntTypeDecoder();
            this.boolTypeDecoder = new BoolTypeDecoder();    
        }

        public override bool IsSupportedType(Type type)
        {
            return type == typeof (byte[]) || type == typeof (string) || intTypeDecoder.IsSupportedType(type)
                   || type == typeof (bool) || type == typeof (object);
        }

   
        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");

            if (type == typeof(byte[]) || type == typeof(object))
            {
                return encoded;
            }

            if (type == typeof(string))
            {
                return DecodeString(encoded);
            }

            if (intTypeDecoder.IsSupportedType(type))
            {
                return intTypeDecoder.Decode(encoded, type);
            }

            if (boolTypeDecoder.IsSupportedType(type))
            {
                return boolTypeDecoder.Decode(encoded, type);
            }

            throw new NotSupportedException();
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof (byte[]);
        }


        private string DecodeString(byte[] encoded)
        {
            return System.Text.Encoding.UTF8.GetString(encoded, 0, encoded.Length);
        }
    
    }
}