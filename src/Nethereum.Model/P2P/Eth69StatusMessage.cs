using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/69 Status (0x00). Differs from eth/68 in two ways:
    /// - TotalDifficulty is REMOVED (post-merge consensus is no longer driven by PoW work).
    /// - BestHash is REPLACED with three block-range fields announcing the
    ///   contiguous block range the peer can serve: [EarliestBlock, LatestBlock]
    ///   plus LatestBlockHash.
    ///
    /// Wire layout: rlp([protocolVersion, networkID, genesisHash, forkID, earliestBlock, latestBlock, latestBlockHash])
    /// where forkID = [forkHash(4-byte), forkNext(uint64)].
    /// </summary>
    public class Eth69StatusMessage
    {
        public int ProtocolVersion { get; set; }
        public ulong NetworkId { get; set; }
        public byte[] GenesisHash { get; set; }
        public uint ForkHash { get; set; }
        public ulong ForkNext { get; set; }
        public ulong EarliestBlock { get; set; }
        public ulong LatestBlock { get; set; }
        public byte[] LatestBlockHash { get; set; }
    }

    public static class Eth69StatusMessageEncoder
    {
        public static byte[] Encode(Eth69StatusMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.ProtocolVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.NetworkId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.GenesisHash),
                ForkIdEncoder.Encode(msg.ForkHash, msg.ForkNext),
                RLP.RLP.EncodeElement(((long)msg.EarliestBlock).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.LatestBlock).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.LatestBlockHash)
            );
        }

        public static Eth69StatusMessage Decode(byte[] data)
        {
            var items = (RLPCollection)RLP.RLP.Decode(data);

            var forkIdItems = (RLPCollection)items[3];
            ForkIdEncoder.DecodeItems(forkIdItems, out var forkHash, out var forkNext);

            return new Eth69StatusMessage
            {
                ProtocolVersion = items[0].RLPData.ToIntFromRLPDecoded(),
                NetworkId = (ulong)items[1].RLPData.ToLongFromRLPDecoded(),
                GenesisHash = items[2].RLPData,
                ForkHash = forkHash,
                ForkNext = forkNext,
                EarliestBlock = (ulong)items[4].RLPData.ToLongFromRLPDecoded(),
                LatestBlock = (ulong)items[5].RLPData.ToLongFromRLPDecoded(),
                LatestBlockHash = items[6].RLPData
            };
        }
    }
}
