using System;
using Nethereum.Freezer;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    public sealed class HashesItemCodec : IItemCodec<byte[]>
    {
        public byte[] Encode(byte[] item) => item;

        public byte[] Decode(ReadOnlySpan<byte> bytes) => bytes.ToArray();
    }
}
