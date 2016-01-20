using System;

namespace Ethereum.RPC.ABI
{
    public interface ITypeDecoder
    {
        bool IsSupportedType(Type type);
        object Decode(byte[] encoded, Type type);

        T Decode<T>(byte[] encoded);

        object Decode(string hexString, Type type);

        T Decode<T>(string hexString);
    }
}