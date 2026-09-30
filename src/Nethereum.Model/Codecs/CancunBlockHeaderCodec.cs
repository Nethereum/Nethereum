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
            var collection = (RLPCollection)RLP.RLP.Decode(rawBytes);
            if (collection.Count != 20)
                throw new System.InvalidOperationException(
                    $"Cancun header codec expects 20 fields, got {collection.Count}");

            return new BlockHeader
            {
                ParentHash = collection[0].RLPData,
                UnclesHash = collection[1].RLPData,
                Coinbase = collection[2].RLPData.ToHex(),
                StateRoot = collection[3].RLPData,
                TransactionsHash = collection[4].RLPData,
                ReceiptHash = collection[5].RLPData,
                LogsBloom = collection[6].RLPData,
                Difficulty = collection[7].RLPData.ToEvmUInt256FromRLPDecoded(),
                BlockNumber = collection[8].RLPData.ToEvmUInt256FromRLPDecoded(),
                GasLimit = collection[9].RLPData.ToLongFromRLPDecoded(),
                GasUsed = collection[10].RLPData.ToLongFromRLPDecoded(),
                Timestamp = collection[11].RLPData.ToLongFromRLPDecoded(),
                ExtraData = collection[12].RLPData,
                MixHash = collection[13].RLPData,
                Nonce = collection[14].RLPData,
                BaseFee = collection[15].RLPData.ToEvmUInt256FromRLPDecoded(),
                WithdrawalsRoot = collection[16].RLPData,
                BlobGasUsed = unchecked((long)collection[17].RLPData.ToULongFromRLPDecoded()),
                ExcessBlobGas = unchecked((long)collection[18].RLPData.ToULongFromRLPDecoded()),
                ParentBeaconBlockRoot = collection[19].RLPData,
            };
        }
    }
}
