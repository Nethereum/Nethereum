using System;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class BlockHeaderEncoder
    {
        public static BlockHeaderEncoder Current { get; } = new BlockHeaderEncoder();

        public byte[] Encode(BlockHeader header, bool legacyMode = false)
        {
            return SelectForEncode(header, legacyMode).Encode(header);
        }

        private const int CliqueSealSuffixLength = 65;

        public byte[] EncodeCliqueSigHeader(BlockHeader header, bool legacyMode = false)
        {
            var sealed_ = header.ShallowCopy();
            sealed_.ExtraData = header.ExtraData == null
                ? null
                : header.ExtraData.Slice(0, header.ExtraData.Length - CliqueSealSuffixLength);

            return SelectForEncode(sealed_, legacyMode).Encode(sealed_);
        }

        public byte[] EncodeCliqueSigHeaderAndHash(BlockHeader header, bool legacyMode = false)
        {
            return new Sha3Keccack().CalculateHash(EncodeCliqueSigHeader(header, legacyMode));
        }

        public BlockHeader Decode(byte[] rawdata, bool legacyMode = false)
        {
            if (legacyMode) return LegacyBlockHeaderCodec.Instance.Decode(rawdata);

            return BlockHeaderRlpDecoder.Decode(rawdata);
        }

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-header-reader", "BlockHeaderEncoder.Decode(RlpReader)")]
        public BlockHeader Decode(RLP.RlpReader reader)
        {
            return BlockHeaderRlpDecoder.Read(reader);
        }

        private static IBlockHeaderCodec SelectForEncode(BlockHeader header, bool legacyMode)
        {
            return legacyMode
                ? LegacyBlockHeaderCodec.Instance
                : BlockHeaderCodecSelector.ForHeader(header);
        }
    }
}
