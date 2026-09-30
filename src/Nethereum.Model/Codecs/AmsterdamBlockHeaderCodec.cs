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
            var collection = (RLPCollection)RLP.RLP.Decode(rawBytes);
            if (collection.Count != 23)
                throw new System.InvalidOperationException(
                    $"Amsterdam header codec expects 23 fields, got {collection.Count}");

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
                RequestsHash = collection[20].RLPData,
                BlockAccessListHash = collection[21].RLPData,
                SlotNumber = collection[22].RLPData.ToULongFromRLPDecoded(),
            };
        }
    }
}
