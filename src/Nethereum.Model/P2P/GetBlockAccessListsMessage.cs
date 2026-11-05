using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/71 GetBlockAccessLists (0x12) per EIP-8159:
    /// <c>[requestId, [blockHash...]]</c>.
    /// </summary>
    public class GetBlockAccessListsMessage
    {
        public ulong RequestId { get; set; }
        public byte[][] BlockHashes { get; set; }
    }

    public static class GetBlockAccessListsMessageEncoder
    {
        public static GetBlockAccessListsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new GetBlockAccessListsMessage
            {
                RequestId = reader.ReadUInt64(),
                BlockHashes = PeerRlp.ReadHashes(reader)
            };
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }

        public static byte[] Encode(GetBlockAccessListsMessage msg)
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
