using System;

namespace Nethereum.Freezer
{
    public sealed class IdentityByteCodec : IItemCodec<byte[]>
    {
        public byte[] Encode(byte[] item) => item;

        public byte[] Decode(ReadOnlySpan<byte> bytes) => bytes.ToArray();
    }
}
