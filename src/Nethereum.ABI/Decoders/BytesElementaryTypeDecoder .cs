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

            var returnArray = encoded.Take(_size).ToArray();

            if (_size == 1 && type == typeof(byte))
            {
                return returnArray[0];
            }

            return returnArray;
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(byte[]);
        }

        public override bool IsSupportedType(Type type)
        {
            if (_size == 1) return (type == typeof(byte[]) || type == typeof(byte));
            return (type == typeof(byte[]));
        }
    }
}