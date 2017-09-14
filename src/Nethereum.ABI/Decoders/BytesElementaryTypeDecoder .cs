using System;
using System.Linq;

namespace Nethereum.ABI.Decoders
{
    public class BytesElementaryTypeDecoder : TypeDecoder
    {
        private readonly int _size;

        public BytesElementaryTypeDecoder(int size)
        {
            this._size = size;
        }

        public override object Decode(byte[] encoded, Type type)
        {
            if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            
            return encoded.Take(_size).ToArray();
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(byte[]);
        }

        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(byte[]));
        }
    }
}