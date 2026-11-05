using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Shanghai only. Adds <c>withdrawalsRoot</c>
    /// (EIP-4895). Exactly 17 fields.
    /// </summary>
    public sealed class ShanghaiBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly ShanghaiBlockHeaderCodec Instance = new ShanghaiBlockHeaderCodec();

        public bool CarriesBaseFee => true;

        /// <summary>EIP-4895: this is the fork that introduces it.</summary>
        public bool CarriesWithdrawalsRoot => true;

        public bool CarriesBlobFieldsAndBeaconRoot => false;
        public bool CarriesRequestsHash => false;
        public bool CarriesBlockAccessList => false;

        public string ShapeName => "Shanghai";

        public byte[] Encode(BlockHeader header)
        {
            if (header.BaseFee == null)
                throw new System.ArgumentException("BaseFee must be set on London-onward headers (EIP-1559).", nameof(header));
            if (header.WithdrawalsRoot == null)
                throw new System.ArgumentException("WithdrawalsRoot must be set on Shanghai-onward headers (EIP-4895).", nameof(header));

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
                header.WithdrawalsRoot,
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 17, "Shanghai");
        }
    }
}
