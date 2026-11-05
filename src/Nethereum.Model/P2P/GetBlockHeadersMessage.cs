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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new GetBlockHeadersMessage
            {
                RequestId = reader.ReadUInt64()
            };

            var inner = reader.ReadListHeader();
            if (reader.PeekSize() == PeerRlp.HashLength)
                msg.StartBlockHash = reader.ReadFixed(PeerRlp.HashLength);
            else
                msg.StartBlock = reader.ReadUInt64();
            msg.Limit = reader.ReadUInt64();
            msg.Skip = reader.ReadUInt64();
            msg.Reverse = reader.ReadBool();
            reader.EnsureEnd(inner);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
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
