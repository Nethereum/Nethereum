using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class BlockRangeUpdateMessage
    {
        public ulong EarliestBlock { get; set; }
        public ulong LatestBlock { get; set; }
        public byte[] LatestBlockHash { get; set; } = new byte[32];
    }

    public static class BlockRangeUpdateMessageEncoder
    {
        public static byte[] Encode(BlockRangeUpdateMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.EarliestBlock).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.LatestBlock).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.LatestBlockHash)
            );
        }

        public static BlockRangeUpdateMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new BlockRangeUpdateMessage
            {
                EarliestBlock = reader.ReadUInt64(),
                LatestBlock = reader.ReadUInt64(),
                LatestBlockHash = reader.ReadFixed(PeerRlp.HashLength)
            };
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }
    }
}
