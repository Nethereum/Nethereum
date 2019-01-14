using System;
using System.Linq;
using System.Text;

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

            if (_size == 16 && type == typeof(Guid))
            {
                return new Guid(returnArray);
            }

            if (type == typeof(string))
                return DecodeString(encoded);

            return returnArray;
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(byte[]);
        }

        public override bool IsSupportedType(Type type)
        {
            bool specialTypeSupported = false;
            if (_size == 1 && type == typeof(byte)) specialTypeSupported = true;
            if (_size == 16 && type == typeof(Guid)) specialTypeSupported = true;
            return (type == typeof(byte[]) || type == typeof(string) || type == typeof(object) || specialTypeSupported);
        }

        private string DecodeString(byte[] encoded)
        {
            return Encoding.UTF8.GetString(encoded, 0, encoded.Length).TrimEnd('\0');
        }
    }
}