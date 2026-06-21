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
            var collection = (RLPCollection)RLP.RLP.Decode(rawBytes);
            if (collection.Count != 15)
                throw new System.InvalidOperationException(
                    $"Pre-London header codec expects 15 fields, got {collection.Count}");

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
            };
        }
    }
}
