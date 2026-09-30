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

            var decoded = RLP.RLP.Decode(rawdata) as RLP.RLPCollection;
            if (decoded == null)
                throw new ArgumentException("Block header RLP is not a list.", nameof(rawdata));

            var codec = BlockHeaderCodecSelector.ForFieldCount(decoded.Count);
            if (codec == null)
                throw new ArgumentException(
                    $"A block header with {decoded.Count} fields matches no fork this build knows. " +
                    "Decoding it as the nearest shorter fork would discard the trailing fields, and the " +
                    "header would then re-encode to different bytes and fail its own hash check.",
                    nameof(rawdata));

            return codec.Decode(rawdata);
        }

        private static IBlockHeaderCodec SelectForEncode(BlockHeader header, bool legacyMode)
        {
            return legacyMode
                ? LegacyBlockHeaderCodec.Instance
                : BlockHeaderCodecSelector.ForHeader(header);
        }
    }
}
