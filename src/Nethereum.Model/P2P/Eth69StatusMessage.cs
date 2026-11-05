using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
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
                RLP.RLP.EncodeElement(((long)(uint)msg.ProtocolVersion).ToBytesForRLPEncoding()),
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new Eth69StatusMessage
            {
                ProtocolVersion = unchecked((int)PeerRlp.ReadUInt32(reader)),
                NetworkId = reader.ReadUInt64(),
                GenesisHash = reader.ReadFixed(PeerRlp.HashLength)
            };
            ForkIdEncoder.Read(reader, out var forkHash, out var forkNext);
            message.ForkHash = forkHash;
            message.ForkNext = forkNext;
            message.EarliestBlock = reader.ReadUInt64();
            message.LatestBlock = reader.ReadUInt64();
            message.LatestBlockHash = reader.ReadFixed(PeerRlp.HashLength);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }
    }
}
