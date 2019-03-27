using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer;

namespace Nethereum.Model
{
    public class BlockHeaderEncoder
    {
        public static BlockHeaderEncoder Current { get; } = new BlockHeaderEncoder();


        public byte[] EncodeCliqueSigHeaderAndHash(BlockHeader header)
        {
            return new Util.Sha3Keccack().CalculateHash(EncodeCliqueSigHeader(header));
        }

        public string RecoverCliqueSigner(BlockHeader blockHeader)
        {
            var blockEncoded = EncodeCliqueSigHeader(blockHeader);
            var signature = blockHeader.ExtraData.Skip(blockHeader.ExtraData.Length - 65).ToArray();
            return
                new MessageSigner().EcRecover(BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(blockHeader),
                    signature.ToHex());
        }

        public byte[] EncodeCliqueSigHeader(BlockHeader header)
        {
            return RLP.RLP.EncodeElementsAndList(
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
            );
        }

        public byte[] Encode(BlockHeader header)
        {
            return RLP.RLP.EncodeElementsAndList(
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
            );
        }

        public BlockHeader Decode(byte[] rawdata)
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
            return blockHeader;
        }
    }
}