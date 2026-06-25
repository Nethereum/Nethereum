using System;
using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class Eth68StatusMessage
    {
        public int ProtocolVersion { get; set; }
        public ulong NetworkId { get; set; }
        public BigInteger TotalDifficulty { get; set; }
        public byte[] BestHash { get; set; }
        public byte[] GenesisHash { get; set; }
        public uint ForkHash { get; set; }
        public ulong ForkNext { get; set; }
    }

    public static class Eth68StatusMessageEncoder
    {
        public static byte[] Encode(Eth68StatusMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.ProtocolVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.NetworkId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.TotalDifficulty.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.BestHash),
                RLP.RLP.EncodeElement(msg.GenesisHash),
                ForkIdEncoder.Encode(msg.ForkHash, msg.ForkNext)
            );
        }

        public static Eth68StatusMessage Decode(byte[] data)
        {
            var items = (RLPCollection)RLP.RLP.Decode(data);

            var forkIdItems = (RLPCollection)items[5];
            ForkIdEncoder.DecodeItems(forkIdItems, out var forkHash, out var forkNext);

            return new Eth68StatusMessage
            {
                ProtocolVersion = items[0].RLPData.ToIntFromRLPDecoded(),
                NetworkId = (ulong)items[1].RLPData.ToLongFromRLPDecoded(),
                TotalDifficulty = items[2].RLPData.ToBigIntegerFromRLPDecoded(),
                BestHash = items[3].RLPData,
                GenesisHash = items[4].RLPData,
                ForkHash = forkHash,
                ForkNext = forkNext
            };
        }
    }
}
