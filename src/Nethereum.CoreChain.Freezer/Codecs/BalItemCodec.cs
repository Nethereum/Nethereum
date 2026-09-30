using System;
using Nethereum.Freezer;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    public sealed class BalItemCodec : IItemCodec<byte[]>
    {
        public byte[] Encode(byte[] item) => item ?? Array.Empty<byte>();

        public byte[] Decode(ReadOnlySpan<byte> bytes) => bytes.ToArray();
    }
}
