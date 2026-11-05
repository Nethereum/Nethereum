using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Cancun. Adds <c>blobGasUsed</c>,
    /// <c>excessBlobGas</c> (EIP-4844), and <c>parentBeaconBlockRoot</c>
    /// (EIP-4788). Exactly 20 fields.
    /// </summary>
    public sealed class CancunBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly CancunBlockHeaderCodec Instance = new CancunBlockHeaderCodec();

        public bool CarriesBaseFee => true;
        public bool CarriesWithdrawalsRoot => true;

        /// <summary>EIP-4844 and EIP-4788: this is the fork that introduces them.</summary>
        public bool CarriesBlobFieldsAndBeaconRoot => true;
        public bool CarriesRequestsHash => false;
        public bool CarriesBlockAccessList => false;

        public string ShapeName => "Cancun";

        public byte[] Encode(BlockHeader header)
        {
            if (header.BaseFee == null)
                throw new System.ArgumentException("BaseFee must be set on London-onward headers (EIP-1559).", nameof(header));
            if (header.WithdrawalsRoot == null)
                throw new System.ArgumentException("WithdrawalsRoot must be set on Shanghai-onward headers (EIP-4895).", nameof(header));
            if (header.ParentBeaconBlockRoot == null)
                throw new System.ArgumentException("ParentBeaconBlockRoot must be set on Cancun-onward headers (EIP-4788).", nameof(header));

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
                (header.BlobGasUsed ?? 0).ToBytesForRLPEncoding(),
                (header.ExcessBlobGas ?? 0).ToBytesForRLPEncoding(),
                header.ParentBeaconBlockRoot,
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 20, "Cancun");
        }
    }
}
