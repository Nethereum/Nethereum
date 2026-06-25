using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Block-header codec for Prague onward (also covers Osaka and
    /// OsakaBpo1, which inherit the Prague field set). Adds
    /// <c>requestsHash</c> (EIP-7685). Exactly 21 fields.
    /// </summary>
    public sealed class PragueBlockHeaderCodec : IBlockHeaderCodec
    {
        public static readonly PragueBlockHeaderCodec Instance = new PragueBlockHeaderCodec();

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
            var collection = (RLPCollection)RLP.RLP.Decode(rawBytes);
            if (collection.Count != 21)
                throw new System.InvalidOperationException(
                    $"Prague header codec expects 21 fields, got {collection.Count}");

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
                BlobGasUsed = collection[17].RLPData.ToLongFromRLPDecoded(),
                ExcessBlobGas = collection[18].RLPData.ToLongFromRLPDecoded(),
                ParentBeaconBlockRoot = collection[19].RLPData,
                RequestsHash = collection[20].RLPData,
            };
        }
    }
}
