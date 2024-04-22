using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Decoders
{
    public abstract class TypeDecoder : ITypeDecoder
    {
        public abstract bool IsSupportedType(Type type);
        public abstract object Decode(byte[] encoded, Type type);

        public abstract object DecodePacked(byte[] encoded, Type type);

        public T DecodePacked<T>(byte[] encoded)
        {
            return (T) DecodePacked(encoded, typeof(T));
        }

        public object DecodePacked(string encoded, Type type)
        {
            encoded = encoded.EnsureHexPrefix();
            return DecodePacked(encoded.HexToByteArray(), type);
        }

        public T Decode<T>(byte[] encoded)
        {
            return (T) Decode(encoded, typeof(T));
        }

        public object Decode(string encoded, Type type)
        {
            encoded = encoded.EnsureHexPrefix();
            return Decode(encoded.HexToByteArray(), type);
        }

        public T Decode<T>(string encoded)
        {
            return (T) Decode(encoded, typeof(T));
        }

        public abstract Type GetDefaultDecodingType();

   

        public T DecodePacked<T>(string hexString)
        {
            return DecodePacked<T>(hexString.HexToByteArray());
        }
    }
}