using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class BlockAccessListsMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> BlockAccessListsByBlock { get; set; } = new();
    }

    public static class BlockAccessListsMessageEncoder
    {
        public static byte[] Encode(BlockAccessListsMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(BlockAccessListWireEntries.ForWire(msg.BlockAccessListsByBlock))
            );
        }

        public static BlockAccessListsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);

            var msg = new BlockAccessListsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var balList = (RLPCollection)outer[1];
            foreach (var balRlp in balList)
            {
                msg.BlockAccessListsByBlock.Add(balRlp.RLPData ?? new byte[0]);
            }

            return msg;
        }
    }
}
