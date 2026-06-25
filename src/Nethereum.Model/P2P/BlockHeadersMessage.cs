using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class BlockHeadersMessage
    {
        public ulong RequestId { get; set; }
        public List<BlockHeader> Headers { get; set; } = new();
    }

    public static class BlockHeadersMessageEncoder
    {
        public static byte[] Encode(BlockHeadersMessage msg)
        {
            var headerEncoder = BlockHeaderEncoder.Current;
            var encodedHeaders = new byte[msg.Headers.Count][];
            for (int i = 0; i < msg.Headers.Count; i++)
                encodedHeaders[i] = headerEncoder.Encode(msg.Headers[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedHeaders)
            );
        }

        public static BlockHeadersMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);

            var msg = new BlockHeadersMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var headersList = (RLPCollection)outer[1];
            var encoder = BlockHeaderEncoder.Current;

            foreach (RLPCollection headerRlp in headersList)
            {
                var header = encoder.Decode(headerRlp.RLPData);
                msg.Headers.Add(header);
            }

            return msg;
        }
    }
}
