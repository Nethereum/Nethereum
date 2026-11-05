using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Prague onward (also covers Osaka, OsakaBpo1,
    /// and OsakaBpo2, all of which inherit the Prague field set). Adds
    /// <c>requestsHash</c> (EIP-7685). Exactly 21 fields.
    /// </summary>
    public sealed class PragueBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly PragueBlockHeaderCodec Instance = new PragueBlockHeaderCodec();

        public bool CarriesBaseFee => true;
        public bool CarriesWithdrawalsRoot => true;
        public bool CarriesBlobFieldsAndBeaconRoot => true;

        /// <summary>EIP-7685: this is the fork that introduces it.</summary>
        public bool CarriesRequestsHash => true;
        public bool CarriesBlockAccessList => false;

        public string ShapeName => "Prague";

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
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public BlockHeader Decode(byte[] rawBytes)
        {
            return BlockHeaderRlpDecoder.Decode(rawBytes, 21, "Prague");
        }
    }
}
