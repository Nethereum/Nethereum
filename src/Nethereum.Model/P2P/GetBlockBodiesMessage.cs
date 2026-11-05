using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class GetBlockBodiesMessage
    {
        public ulong RequestId { get; set; }
        public byte[][] BlockHashes { get; set; }
    }

    public static class GetBlockBodiesMessageEncoder
    {
        public static GetBlockBodiesMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new GetBlockBodiesMessage
            {
                RequestId = reader.ReadUInt64(),
                BlockHashes = PeerRlp.ReadHashes(reader)
            };
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }

        public static byte[] Encode(GetBlockBodiesMessage msg)
        {
            var hashElements = new byte[msg.BlockHashes.Length][];
            for (int i = 0; i < msg.BlockHashes.Length; i++)
                hashElements[i] = RLP.RLP.EncodeElement(msg.BlockHashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(hashElements)
            );
        }
    }
}
