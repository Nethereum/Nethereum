using System;

namespace Nethereum.Freezer
{
    public interface IItemCodec<T>
    {
        byte[] Encode(T item);
        T Decode(ReadOnlySpan<byte> bytes);
    }
}
