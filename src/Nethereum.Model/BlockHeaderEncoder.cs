using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;


namespace Nethereum.Model
{
    public class BlockHeaderEncoder
    {
        public static BlockHeaderEncoder Current { get; } = new BlockHeaderEncoder();

        private byte[][] GetBaseFields(BlockHeader header)
        {
            return new byte[][]
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
                header.Nonce
            };
        }

        private byte[][] GetBaseFieldsCliqueSig(BlockHeader header)
        {
            return new byte[][]
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
                header.ExtraData.Take(header.ExtraData.Length - 65).ToArray(),
                header.MixHash,
                header.Nonce
            };
        }

        public byte[] EncodeCliqueSigHeaderAndHash(BlockHeader header, bool legacyMode = false)
        {
            return new Util.Sha3Keccack().CalculateHash(EncodeCliqueSigHeader(header, legacyMode));
        }

        public byte[] EncodeCliqueSigHeader(BlockHeader header, bool legacyMode = false)
        {
            var fields = new List<byte[]>(GetBaseFieldsCliqueSig(header));

            if (!legacyMode && header.BaseFee != null)
            {
                fields.Add(header.BaseFee.Value.ToBytesForRLPEncoding());

                if (header.WithdrawalsRoot != null)
                {
                    fields.Add(header.WithdrawalsRoot);

                    if (header.ParentBeaconBlockRoot != null)
                    {
                        fields.Add((header.BlobGasUsed ?? 0).ToBytesForRLPEncoding());
                        fields.Add((header.ExcessBlobGas ?? 0).ToBytesForRLPEncoding());
                        fields.Add(header.ParentBeaconBlockRoot);

                        if (header.RequestsHash != null)
                        {
                            fields.Add(header.RequestsHash);
                        }
                    }
                }
            }

            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields.ToArray());
        }

        public byte[] Encode(BlockHeader header, bool legacyMode = false)
        {
            var fields = new List<byte[]>(GetBaseFields(header));

            if (!legacyMode && header.BaseFee != null)
            {
                fields.Add(header.BaseFee.Value.ToBytesForRLPEncoding());

                if (header.WithdrawalsRoot != null)
                {
                    fields.Add(header.WithdrawalsRoot);

                    if (header.ParentBeaconBlockRoot != null)
                    {
                        fields.Add((header.BlobGasUsed ?? 0).ToBytesForRLPEncoding());
                        fields.Add((header.ExcessBlobGas ?? 0).ToBytesForRLPEncoding());
                        fields.Add(header.ParentBeaconBlockRoot);

                        if (header.RequestsHash != null)
                        {
                            fields.Add(header.RequestsHash);
                        }
                    }
                }
            }

            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields.ToArray());
        }

        public BlockHeader Decode(byte[] rawdata, bool legacyMode = false)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedElements = (RLPCollection)decodedList;

            var blockHeader = new BlockHeader();
            blockHeader.ParentHash = decodedElements[0].RLPData;
            blockHeader.UnclesHash = decodedElements[1].RLPData;
            blockHeader.Coinbase = decodedElements[2].RLPData.ToHex();
            blockHeader.StateRoot = decodedElements[3].RLPData;
            blockHeader.TransactionsHash = decodedElements[4].RLPData;
            blockHeader.ReceiptHash = decodedElements[5].RLPData;
            blockHeader.LogsBloom = decodedElements[6].RLPData;
            blockHeader.Difficulty = decodedElements[7].RLPData.ToBigIntegerFromRLPDecoded();
            blockHeader.BlockNumber = decodedElements[8].RLPData.ToBigIntegerFromRLPDecoded();
            blockHeader.GasLimit = decodedElements[9].RLPData.ToLongFromRLPDecoded();
            blockHeader.GasUsed = decodedElements[10].RLPData.ToLongFromRLPDecoded();
            blockHeader.Timestamp = decodedElements[11].RLPData.ToLongFromRLPDecoded();
            blockHeader.ExtraData = decodedElements[12].RLPData;
            blockHeader.MixHash = decodedElements[13].RLPData;
            blockHeader.Nonce = decodedElements[14].RLPData;

            if (legacyMode) return blockHeader;

            var count = decodedElements.Count;

            // London (16 fields)
            if (count >= 16)
            {
                blockHeader.BaseFee = decodedElements[15].RLPData.ToBigIntegerFromRLPDecoded();
            }

            // Shanghai (17 fields)
            if (count >= 17)
            {
                blockHeader.WithdrawalsRoot = decodedElements[16].RLPData;
            }

            // Cancun (20 fields)
            if (count >= 20)
            {
                blockHeader.BlobGasUsed = decodedElements[17].RLPData.ToLongFromRLPDecoded();
                blockHeader.ExcessBlobGas = decodedElements[18].RLPData.ToLongFromRLPDecoded();
                blockHeader.ParentBeaconBlockRoot = decodedElements[19].RLPData;
            }

            // Prague (21 fields)
            if (count >= 21)
            {
                blockHeader.RequestsHash = decodedElements[20].RLPData;
            }

            return blockHeader;
        }
    }
}
