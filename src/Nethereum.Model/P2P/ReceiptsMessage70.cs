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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new ReceiptsMessage70
            {
                RequestId = reader.ReadUInt64(),
                LastBlockIncomplete = reader.ReadBool()
            };
            PeerRlp.ReadReceiptBlocks(reader, ReceiptsMessageEth69Encoder.ReadSingleReceipt, msg.ReceiptsByBlock);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
