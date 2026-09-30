using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class ReceiptsMessage70
    {
        public ulong RequestId { get; set; }
        public bool LastBlockIncomplete { get; set; }
        public List<List<Receipt>> ReceiptsByBlock { get; set; } = new();
    }

    public static class ReceiptsMessageEth70Encoder
    {
        public static byte[] Encode(ReceiptsMessage70 msg)
        {
            var encodedBlocks = new byte[msg.ReceiptsByBlock.Count][];
            for (int i = 0; i < msg.ReceiptsByBlock.Count; i++)
                encodedBlocks[i] = ReceiptsMessageEth69Encoder.EncodeBlockReceipts(msg.ReceiptsByBlock[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.LastBlockIncomplete
                    ? new byte[] { 0x01 }
                    : new byte[0]),
                RLP.RLP.EncodeList(encodedBlocks));
        }

        public static ReceiptsMessage70 Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var incompleteData = outer[1].RLPData ?? new byte[0];

            var msg = new ReceiptsMessage70
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                LastBlockIncomplete = incompleteData.Length > 0 && incompleteData[0] != 0
            };

            var blockList = (RLPCollection)outer[2];
            foreach (RLPCollection blockReceipts in blockList)
            {
                if (PeerResponseBounds.ExceedsWhatABlockCanHold(blockReceipts)) break;
                msg.ReceiptsByBlock.Add(ReceiptsMessageEth69Encoder.DecodeBlockReceipts(blockReceipts));
            }

            return msg;
        }
    }
}
