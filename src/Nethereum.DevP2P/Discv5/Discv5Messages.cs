using System;
using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// discv5 protocol message types per
    /// https://github.com/ethereum/devp2p/blob/master/discv5/discv5-wire.md
    /// </summary>
    public enum Discv5MessageType : byte
    {
        Ping = 0x01,
        Pong = 0x02,
        FindNode = 0x03,
        Nodes = 0x04,
        TalkReq = 0x05,
        TalkResp = 0x06,
        RegTopic = 0x07,
        Ticket = 0x08,
        RegConfirmation = 0x09,
        TopicQuery = 0x0A
    }

    public class Discv5PingMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        public ulong EnrSeq { get; set; }
    }

    public class Discv5PongMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        public ulong EnrSeq { get; set; }
        public byte[] RecipientIp { get; set; } = Array.Empty<byte>(); // 4 or 16 bytes
        public ushort RecipientPort { get; set; }
    }

    public class Discv5FindNodeMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        public List<ulong> Distances { get; set; } = new();
    }

    public class Discv5NodesMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        /// <summary>
        /// Total number of NODES responses to expect for this request. Per
        /// discv5-wire.md §"NODES message" this is a <c>uint8</c> — a multi-byte
        /// value would be decoded as a different RLP shape and rejected by spec-
        /// conformant peers.
        /// </summary>
        public byte Total { get; set; }
        /// <summary>RLP-encoded ENR records.</summary>
        public List<byte[]> Records { get; set; } = new();
    }

    public class Discv5TalkReqMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        public byte[] Protocol { get; set; } = Array.Empty<byte>();
        public byte[] Request { get; set; } = Array.Empty<byte>();
    }

    public class Discv5TalkRespMessage
    {
        public byte[] RequestId { get; set; } = Array.Empty<byte>();
        public byte[] Response { get; set; } = Array.Empty<byte>();
    }

    public static class Discv5MessageEncoder
    {
        public static byte[] EncodePing(Discv5PingMessage m) =>
            PrefixType(Discv5MessageType.Ping, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeElement(LongToRlp((long)m.EnrSeq))));

        public static Discv5PingMessage DecodePing(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            return new Discv5PingMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>(),
                EnrSeq = (ulong)(o[1].RLPData ?? Array.Empty<byte>()).ToLongFromRLPDecoded()
            };
        }

        public static byte[] EncodePong(Discv5PongMessage m) =>
            PrefixType(Discv5MessageType.Pong, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeElement(LongToRlp((long)m.EnrSeq)),
                RLP.RLP.EncodeElement(m.RecipientIp),
                RLP.RLP.EncodeElement(PortToRlp(m.RecipientPort))));

        public static Discv5PongMessage DecodePong(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            return new Discv5PongMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>(),
                EnrSeq = (ulong)(o[1].RLPData ?? Array.Empty<byte>()).ToLongFromRLPDecoded(),
                RecipientIp = o[2].RLPData ?? Array.Empty<byte>(),
                RecipientPort = PortFromRlp(o[3].RLPData)
            };
        }

        public static byte[] EncodeFindNode(Discv5FindNodeMessage m)
        {
            var distEncoded = new byte[m.Distances.Count][];
            for (int i = 0; i < m.Distances.Count; i++)
                distEncoded[i] = RLP.RLP.EncodeElement(LongToRlp((long)m.Distances[i]));

            return PrefixType(Discv5MessageType.FindNode, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeList(distEncoded)));
        }

        public static Discv5FindNodeMessage DecodeFindNode(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            var m = new Discv5FindNodeMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>()
            };
            foreach (var d in (RLPCollection)o[1])
                m.Distances.Add((ulong)(d.RLPData ?? Array.Empty<byte>()).ToLongFromRLPDecoded());
            return m;
        }

        public static byte[] EncodeNodes(Discv5NodesMessage m)
        {
            var records = new byte[m.Records.Count][];
            for (int i = 0; i < m.Records.Count; i++)
                records[i] = m.Records[i];

            return PrefixType(Discv5MessageType.Nodes, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeElement(LongToRlp((long)m.Total)),
                RLP.RLP.EncodeList(records)));
        }

        public static Discv5NodesMessage DecodeNodes(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            var totalLong = (o[1].RLPData ?? Array.Empty<byte>()).ToLongFromRLPDecoded();
            if (totalLong < 0 || totalLong > byte.MaxValue)
                throw new ArgumentException("nodes.total must fit in uint8 per discv5-wire.md");
            var m = new Discv5NodesMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>(),
                Total = (byte)totalLong
            };
            foreach (var r in (RLPCollection)o[2])
            {
                if (r is RLPCollection)
                {
                    // Re-encode each inner element with its RLP prefix so the
                    // resulting bytes round-trip back to a parseable ENR. The
                    // raw RLPData strips the framing during decode — passing
                    // those bytes straight to EncodeList would produce a list
                    // whose payload is the concatenation of un-prefixed values.
                    var sub = (RLPCollection)r;
                    var elements = new List<byte[]>();
                    foreach (var e in sub) elements.Add(RLP.RLP.EncodeElement(e.RLPData));
                    m.Records.Add(RLP.RLP.EncodeList(elements.ToArray()));
                }
                else
                {
                    m.Records.Add(r.RLPData);
                }
            }
            return m;
        }

        public static byte[] EncodeTalkReq(Discv5TalkReqMessage m) =>
            PrefixType(Discv5MessageType.TalkReq, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeElement(m.Protocol),
                RLP.RLP.EncodeElement(m.Request)));

        public static Discv5TalkReqMessage DecodeTalkReq(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            return new Discv5TalkReqMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>(),
                Protocol = o[1].RLPData ?? Array.Empty<byte>(),
                Request = o[2].RLPData ?? Array.Empty<byte>()
            };
        }

        public static byte[] EncodeTalkResp(Discv5TalkRespMessage m) =>
            PrefixType(Discv5MessageType.TalkResp, RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.RequestId),
                RLP.RLP.EncodeElement(m.Response)));

        public static Discv5TalkRespMessage DecodeTalkResp(byte[] body)
        {
            var o = (RLPCollection)RLP.RLP.Decode(body);
            return new Discv5TalkRespMessage
            {
                RequestId = o[0].RLPData ?? Array.Empty<byte>(),
                Response = o[1].RLPData ?? Array.Empty<byte>()
            };
        }

        /// <summary>
        /// discv5 messages are framed as `type_byte || rlp(body)` inside the
        /// encrypted packet payload. This helper builds that frame.
        /// </summary>
        public static byte[] PrefixType(Discv5MessageType type, byte[] rlpBody)
        {
            var packed = new byte[1 + rlpBody.Length];
            packed[0] = (byte)type;
            System.Buffer.BlockCopy(rlpBody, 0, packed, 1, rlpBody.Length);
            return packed;
        }

        /// <summary>
        /// Splits a `type_byte || rlp(body)` packed message back into its components.
        /// </summary>
        public static (Discv5MessageType type, byte[] body) Unpack(byte[] data)
        {
            if (data == null || data.Length == 0)
                return (Discv5MessageType.Ping, Array.Empty<byte>());
            var type = (Discv5MessageType)data[0];
            var body = new byte[data.Length - 1];
            System.Buffer.BlockCopy(data, 1, body, 0, body.Length);
            return (type, body);
        }

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return Array.Empty<byte>();
            return value.ToBytesForRLPEncoding();
        }

        private static byte[] PortToRlp(ushort port)
        {
            if (port == 0) return Array.Empty<byte>();
            return new[] { (byte)((port >> 8) & 0xff), (byte)(port & 0xff) };
        }

        private static ushort PortFromRlp(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            if (data.Length == 1) return data[0];
            return (ushort)((data[0] << 8) | data[1]);
        }
    }
}
