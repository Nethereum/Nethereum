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
            var collection = (RLPCollection)RLP.RLP.Decode(rawBytes);
            if (collection.Count != 17)
                throw new System.InvalidOperationException(
                    $"Shanghai header codec expects 17 fields, got {collection.Count}");

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
            };
        }
    }
}
