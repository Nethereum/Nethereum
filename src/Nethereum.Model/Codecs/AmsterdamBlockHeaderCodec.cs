using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Amsterdam onward. Adds
    /// <c>blockAccessListHash</c> (EIP-7928) and <c>slotNumber</c>
    /// (EIP-7843) to the Prague field set. Exactly 23 fields.
    /// </summary>
    public sealed class AmsterdamBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly AmsterdamBlockHeaderCodec Instance = new AmsterdamBlockHeaderCodec();

        public bool CarriesBaseFee => true;
        public bool CarriesWithdrawalsRoot => true;
        public bool CarriesBlobFieldsAndBeaconRoot => true;
        public bool CarriesRequestsHash => true;

        /// <summary>EIP-7928 and EIP-7843: this is the fork that introduces them.</summary>
        public bool CarriesBlockAccessList => true;

        public string ShapeName => "Amsterdam";

        public byte[] Encode(BlockHeader header)
        {
            if (header.BaseFee == null)
                throw new System.ArgumentException("BaseFee must be set on London-onward headers (EIP-1559).", nameof(header));
            if (header.WithdrawalsRoot == null)
                throw new System.ArgumentException("WithdrawalsRoot must be set on Shanghai-onward headers (EIP-4895).", nameof(header));
            if (header.ParentBeaconBlockRoot == null)
                throw new System.ArgumentException("ParentBeaconBlockRoot must be set on Cancun-onward headers (EIP-4788).", nameof(header));
            if (header.RequestsHash == null)
                throw new System.ArgumentException("RequestsHash must be set on Prague-onward headers (EIP-7685).", nameof(header));
            if (header.BlockAccessListHash == null)
                throw new System.ArgumentException("BlockAccessListHash must be set on Amsterdam-onward headers (EIP-7928).", nameof(header));
            if (header.SlotNumber == null)
                throw new System.ArgumentException("SlotNumber must be set on Amsterdam-onward headers (EIP-7843).", nameof(header));

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
                header.RequestsHash,
                header.BlockAccessListHash,
                header.SlotNumber.Value.ToBytesForRLPEncoding(),
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 23, "Amsterdam");
        }
    }
}
