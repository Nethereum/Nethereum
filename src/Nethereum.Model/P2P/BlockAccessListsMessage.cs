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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new BlockAccessListsMessage { RequestId = reader.ReadUInt64() };
            msg.BlockAccessListsByBlock = PeerRlp.ReadEthBlockAccessLists(reader);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
