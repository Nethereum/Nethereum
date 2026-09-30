using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class GetReceiptsMessage70
    {
        public ulong RequestId { get; set; }
        public ulong FirstBlockReceiptIndex { get; set; }
        public byte[][] BlockHashes { get; set; }
    }

    public static class GetReceiptsMessage70Encoder
    {
        public static GetReceiptsMessage70 Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var hashList = (RLPCollection)outer[2];

            var hashes = new byte[hashList.Count][];
            for (int i = 0; i < hashList.Count; i++)
                hashes[i] = hashList[i].RLPData;

            return new GetReceiptsMessage70
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                FirstBlockReceiptIndex = (ulong)outer[1].RLPData.ToLongFromRLPDecoded(),
                BlockHashes = hashes
            };
        }

        public static byte[] Encode(GetReceiptsMessage70 msg)
        {
            var hashElements = new byte[msg.BlockHashes.Length][];
            for (int i = 0; i < msg.BlockHashes.Length; i++)
                hashElements[i] = RLP.RLP.EncodeElement(msg.BlockHashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((long)msg.FirstBlockReceiptIndex).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(hashElements)
            );
        }
    }
}
