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
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var hashList = (RLPCollection)outer[1];

            var hashes = new byte[hashList.Count][];
            for (int i = 0; i < hashList.Count; i++)
                hashes[i] = hashList[i].RLPData;

            return new GetBlockBodiesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BlockHashes = hashes
            };
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
