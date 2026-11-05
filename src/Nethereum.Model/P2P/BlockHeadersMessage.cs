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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new BlockHeadersMessage { RequestId = reader.ReadUInt64() };

            var headers = reader.ReadListHeader();
            var encoder = BlockHeaderEncoder.Current;
            while (!reader.IsAtEnd(headers))
                msg.Headers.Add(encoder.Decode(reader));

            reader.EnsureEnd(headers);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
