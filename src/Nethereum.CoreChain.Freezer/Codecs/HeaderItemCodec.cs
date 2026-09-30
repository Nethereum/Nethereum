using System;
using Nethereum.Freezer;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    public sealed class HeaderItemCodec : IItemCodec<BlockHeader>
    {
        public byte[] Encode(BlockHeader item) => BlockHeaderEncoder.Current.Encode(item, legacyMode: false);

        public BlockHeader Decode(ReadOnlySpan<byte> bytes) =>
            BlockHeaderEncoder.Current.Decode(bytes.ToArray(), legacyMode: false);
    }
}
