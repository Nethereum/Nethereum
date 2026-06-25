using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/69+ BlockRangeUpdate (0x11): notifies peers when our available block
    /// range changes (e.g., pruned history boundary moves, snap sync completes).
    /// Format: [earliestBlock, latestBlock, latestBlockHash]
    /// </summary>
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
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            return new BlockRangeUpdateMessage
            {
                EarliestBlock = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                LatestBlock = (ulong)outer[1].RLPData.ToLongFromRLPDecoded(),
                LatestBlockHash = outer[2].RLPData
            };
        }
    }
}
