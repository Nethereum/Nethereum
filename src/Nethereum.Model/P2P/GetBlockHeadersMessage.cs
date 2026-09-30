using System;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class GetBlockHeadersMessage
    {
        public ulong RequestId { get; set; }
        public ulong StartBlock { get; set; }
        public byte[] StartBlockHash { get; set; }
        public ulong Limit { get; set; }
        public ulong Skip { get; set; }
        public bool Reverse { get; set; }
    }

    public static class GetBlockHeadersMessageEncoder
    {
        public static GetBlockHeadersMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var inner = (RLPCollection)outer[1];

            var msg = new GetBlockHeadersMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var startData = inner[0].RLPData ?? new byte[0];
            if (startData.Length == 32)
            {
                msg.StartBlockHash = startData;
            }
            else
            {
                msg.StartBlock = (ulong)startData.ToLongFromRLPDecoded();
            }

            msg.Limit = (ulong)inner[1].RLPData.ToLongFromRLPDecoded();
            msg.Skip = (ulong)inner[2].RLPData.ToLongFromRLPDecoded();

            var reverseData = inner[3].RLPData ?? new byte[0];
            msg.Reverse = reverseData.Length > 0 && reverseData[0] != 0;

            return msg;
        }

        public static byte[] Encode(GetBlockHeadersMessage msg)
        {
            byte[] startEncoded = msg.StartBlockHash != null
                ? RLP.RLP.EncodeElement(msg.StartBlockHash)
                : RLP.RLP.EncodeElement(((long)msg.StartBlock).ToBytesForRLPEncoding());

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(
                    startEncoded,
                    RLP.RLP.EncodeElement(((long)msg.Limit).ToBytesForRLPEncoding()),
                    RLP.RLP.EncodeElement(((long)msg.Skip).ToBytesForRLPEncoding()),
                    RLP.RLP.EncodeElement(msg.Reverse
                        ? new byte[] { 0x01 }
                        : new byte[0])
                )
            );
        }
    }
}
