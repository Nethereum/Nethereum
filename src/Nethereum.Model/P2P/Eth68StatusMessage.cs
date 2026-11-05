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
                RLP.RLP.EncodeElement(((long)(uint)msg.ProtocolVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.NetworkId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.TotalDifficulty.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.BestHash),
                RLP.RLP.EncodeElement(msg.GenesisHash),
                ForkIdEncoder.Encode(msg.ForkHash, msg.ForkNext)
            );
        }

        public static Eth68StatusMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new Eth68StatusMessage
            {
                ProtocolVersion = unchecked((int)PeerRlp.ReadUInt32(reader)),
                NetworkId = reader.ReadUInt64(),
                TotalDifficulty = reader.ReadBigInteger(),
                BestHash = reader.ReadFixed(PeerRlp.HashLength),
                GenesisHash = reader.ReadFixed(PeerRlp.HashLength)
            };
            ForkIdEncoder.Read(reader, out var forkHash, out var forkNext);
            message.ForkHash = forkHash;
            message.ForkNext = forkNext;
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }
    }
}
