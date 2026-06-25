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
            var receiptEncoder = ReceiptEncoder.Current;
            var encodedBlocks = new byte[msg.ReceiptsByBlock.Count][];
            for (int i = 0; i < msg.ReceiptsByBlock.Count; i++)
            {
                var blockReceipts = msg.ReceiptsByBlock[i];
                var encodedReceipts = new byte[blockReceipts.Count][];
                for (int j = 0; j < blockReceipts.Count; j++)
                {
                    var r = blockReceipts[j];
                    encodedReceipts[j] = r.TransactionType > 0
                        ? RLP.RLP.EncodeElement(receiptEncoder.EncodeTyped(r, r.TransactionType))
                        : receiptEncoder.Encode(r);
                }
                encodedBlocks[i] = RLP.RLP.EncodeList(encodedReceipts);
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBlocks)
            );
        }

        public static ReceiptsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);

            var msg = new ReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var blockList = (RLPCollection)outer[1];
            var receiptEncoder = ReceiptEncoder.Current;

            foreach (RLPCollection blockReceipts in blockList)
            {
                var receipts = new List<Receipt>();
                foreach (var receiptRlp in blockReceipts)
                {
                    var receipt = receiptEncoder.Decode(receiptRlp.RLPData);
                    receipts.Add(receipt);
                }
                msg.ReceiptsByBlock.Add(receipts);
            }

            return msg;
        }
    }
}
