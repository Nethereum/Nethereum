using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class ReceiptsMessage
    {
        public ulong RequestId { get; set; }
        public List<List<Receipt>> ReceiptsByBlock { get; set; } = new();
    }

    public static class ReceiptsMessageEncoder
    {
        public static byte[] Encode(ReceiptsMessage msg)
        {
            var encodedBlocks = new byte[msg.ReceiptsByBlock.Count][];
            for (int i = 0; i < msg.ReceiptsByBlock.Count; i++)
                encodedBlocks[i] = EncodeBlockReceipts(msg.ReceiptsByBlock[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBlocks)
            );
        }

        public static byte[] EncodeBlockReceipts(List<Receipt> blockReceipts)
        {
            var receiptEncoder = ReceiptEncoder.Current;
            var encodedReceipts = new byte[blockReceipts.Count][];
            for (int j = 0; j < blockReceipts.Count; j++)
            {
                var r = blockReceipts[j];
                encodedReceipts[j] = r.TransactionType > 0
                    ? RLP.RLP.EncodeElement(receiptEncoder.EncodeTyped(r, r.TransactionType))
                    : receiptEncoder.Encode(r);
            }
            return RLP.RLP.EncodeList(encodedReceipts);
        }

        public static ReceiptsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new ReceiptsMessage { RequestId = reader.ReadUInt64() };
            PeerRlp.ReadReceiptBlocks(reader, ReceiptEncoder.Current.Decode, msg.ReceiptsByBlock);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
