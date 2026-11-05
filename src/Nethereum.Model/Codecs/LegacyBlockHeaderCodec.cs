using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Frontier through Berlin (mainnet block 0 to
    /// London-1). Encodes exactly 15 fields per the Yellow Paper. Decode
    /// requires exactly 15 elements; any other count is an error for this
    /// fork.
    /// </summary>
    public sealed class LegacyBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly LegacyBlockHeaderCodec Instance = new LegacyBlockHeaderCodec();

        public bool CarriesBaseFee => false;
        public bool CarriesWithdrawalsRoot => false;
        public bool CarriesBlobFieldsAndBeaconRoot => false;
        public bool CarriesRequestsHash => false;
        public bool CarriesBlockAccessList => false;

        public string ShapeName => "Legacy";

        public byte[] Encode(BlockHeader header)
        {
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
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 15, "Pre-London");
        }
    }
}
