using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class GetReceiptsMessage
    {
        public ulong RequestId { get; set; }
        public byte[][] BlockHashes { get; set; }
    }

    public static class GetReceiptsMessageEncoder
    {
        public static GetReceiptsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new GetReceiptsMessage
            {
                RequestId = reader.ReadUInt64(),
                BlockHashes = PeerRlp.ReadHashes(reader)
            };
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }

        public static byte[] Encode(GetReceiptsMessage msg)
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
