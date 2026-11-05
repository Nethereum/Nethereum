using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for London through Shanghai-1. Adds <c>baseFee</c>
    /// (EIP-1559) to the 15 legacy fields. Exactly 16 fields.
    /// </summary>
    public sealed class LondonBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly LondonBlockHeaderCodec Instance = new LondonBlockHeaderCodec();

        /// <summary>EIP-1559: this is the fork that introduces it.</summary>
        public bool CarriesBaseFee => true;

        public bool CarriesWithdrawalsRoot => false;
        public bool CarriesBlobFieldsAndBeaconRoot => false;
        public bool CarriesRequestsHash => false;
        public bool CarriesBlockAccessList => false;

        public string ShapeName => "London";

        public byte[] Encode(BlockHeader header)
        {
            if (header.BaseFee == null)
                throw new System.ArgumentException(
                    "BaseFee must be set on London-onward headers (EIP-1559).",
                    nameof(header));

            var fields = new byte[][]
            {
                header.ParentHash,
                header.UnclesHash,
                header.Coinbase.HexToByteArray(),
                header.StateRoot,
                header.TransactionsHash,
                header.ReceiptHash,
                header.LogsBloom,
                header.Difficulty.ToBytesForRLPEncoding(),
                header.BlockNumber.ToBytesForRLPEncoding(),
                header.GasLimit.ToBytesForRLPEncoding(),
                header.GasUsed.ToBytesForRLPEncoding(),
                header.Timestamp.ToBytesForRLPEncoding(),
                header.ExtraData,
                header.MixHash,
                header.Nonce,
                header.BaseFee.Value.ToBytesForRLPEncoding(),
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 16, "London..Shanghai-1");
        }
    }
}
