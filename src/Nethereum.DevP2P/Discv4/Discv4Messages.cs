using System.Collections.Generic;
using System.Net;
using Nethereum.RLP;

namespace Nethereum.DevP2P.Discv4
{
    public enum Discv4MessageType : byte
    {
        Ping = 0x01,
        Pong = 0x02,
        FindNode = 0x03,
        Neighbors = 0x04,
        EnrRequest = 0x05,
        EnrResponse = 0x06
    }

    /// <summary>
    /// discv4 Ping (0x01): [version, from, to, expiration, enr-seq?]
    /// </summary>
    public class Discv4PingMessage
    {
        public int Version { get; set; } = 4;
        public Discv4Endpoint From { get; set; } = new Discv4Endpoint();
        public Discv4Endpoint To { get; set; } = new Discv4Endpoint();
        public long Expiration { get; set; }
        public ulong? EnrSeq { get; set; }
    }

    /// <summary>
    /// discv4 Pong (0x02): [to, ping-hash, expiration, enr-seq?]
    /// </summary>
    public class Discv4PongMessage
    {
        public Discv4Endpoint To { get; set; } = new Discv4Endpoint();
        public byte[] PingHash { get; set; } = new byte[32];
        public long Expiration { get; set; }
        public ulong? EnrSeq { get; set; }
    }

    /// <summary>
    /// discv4 FindNode (0x03): [target, expiration]
    /// target is a 64-byte secp256k1 public key (no 0x04 prefix).
    /// </summary>
    public class Discv4FindNodeMessage
    {
        public byte[] Target { get; set; } = new byte[64];
        public long Expiration { get; set; }
    }

    /// <summary>
    /// discv4 Neighbors (0x04): [nodes, expiration]
    /// nodes = [[ip, udp, tcp, nodeId], ...]
    /// </summary>
    public class Discv4NeighborsMessage
    {
        public List<Discv4Neighbor> Nodes { get; set; } = new();
        public long Expiration { get; set; }
    }

    public class Discv4Neighbor
    {
        public IPAddress IP { get; set; } = IPAddress.Loopback;
        public ushort UdpPort { get; set; }
        public ushort TcpPort { get; set; }
        public byte[] NodeId { get; set; } = new byte[64];
    }

    public static class Discv4MessageEncoder
    {
        public static byte[] EncodePing(Discv4PingMessage msg)
        {
            if (msg.EnrSeq.HasValue)
            {
                return RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(IntToRlp(msg.Version)),
                    msg.From.Encode(),
                    msg.To.Encode(),
                    RLP.RLP.EncodeElement(LongToRlp(msg.Expiration)),
                    RLP.RLP.EncodeElement(LongToRlp((long)msg.EnrSeq.Value))
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(IntToRlp(msg.Version)),
                msg.From.Encode(),
                msg.To.Encode(),
                RLP.RLP.EncodeElement(LongToRlp(msg.Expiration))
            );
        }

        public static Discv4PingMessage DecodePing(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new Discv4PingMessage
            {
                Version = (int)outer[0].RLPData.ToLongFromRLPDecoded(),
                From = Discv4Endpoint.Decode((RLPCollection)outer[1]),
                To = Discv4Endpoint.Decode((RLPCollection)outer[2]),
                Expiration = outer[3].RLPData.ToLongFromRLPDecoded()
            };
            if (outer.Count > 4)
                msg.EnrSeq = (ulong)outer[4].RLPData.ToLongFromRLPDecoded();
            return msg;
        }

        public static byte[] EncodePong(Discv4PongMessage msg)
        {
            if (msg.EnrSeq.HasValue)
            {
                return RLP.RLP.EncodeList(
                    msg.To.Encode(),
                    RLP.RLP.EncodeElement(msg.PingHash),
                    RLP.RLP.EncodeElement(LongToRlp(msg.Expiration)),
                    RLP.RLP.EncodeElement(LongToRlp((long)msg.EnrSeq.Value))
                );
            }
            return RLP.RLP.EncodeList(
                msg.To.Encode(),
                RLP.RLP.EncodeElement(msg.PingHash),
                RLP.RLP.EncodeElement(LongToRlp(msg.Expiration))
            );
        }

        public static Discv4PongMessage DecodePong(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new Discv4PongMessage
            {
                To = Discv4Endpoint.Decode((RLPCollection)outer[0]),
                PingHash = outer[1].RLPData,
                Expiration = outer[2].RLPData.ToLongFromRLPDecoded()
            };
            if (outer.Count > 3)
                msg.EnrSeq = (ulong)outer[3].RLPData.ToLongFromRLPDecoded();
            return msg;
        }

        public static byte[] EncodeFindNode(Discv4FindNodeMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(msg.Target),
                RLP.RLP.EncodeElement(LongToRlp(msg.Expiration))
            );
        }

        public static Discv4FindNodeMessage DecodeFindNode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            return new Discv4FindNodeMessage
            {
                Target = outer[0].RLPData,
                Expiration = outer[1].RLPData.ToLongFromRLPDecoded()
            };
        }

        public static byte[] EncodeNeighbors(Discv4NeighborsMessage msg)
        {
            var encodedNodes = new byte[msg.Nodes.Count][];
            for (int i = 0; i < msg.Nodes.Count; i++)
            {
                var n = msg.Nodes[i];
                encodedNodes[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(n.IP.GetAddressBytes()),
                    RLP.RLP.EncodeElement(PortToRlp(n.UdpPort)),
                    RLP.RLP.EncodeElement(PortToRlp(n.TcpPort)),
                    RLP.RLP.EncodeElement(n.NodeId)
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeList(encodedNodes),
                RLP.RLP.EncodeElement(LongToRlp(msg.Expiration))
            );
        }

        public static Discv4NeighborsMessage DecodeNeighbors(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var nodesList = (RLPCollection)outer[0];
            var msg = new Discv4NeighborsMessage
            {
                Expiration = outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection nodeRlp in nodesList)
            {
                msg.Nodes.Add(new Discv4Neighbor
                {
                    IP = new IPAddress(nodeRlp[0].RLPData ?? new byte[4]),
                    UdpPort = PortFromRlp(nodeRlp[1].RLPData),
                    TcpPort = PortFromRlp(nodeRlp[2].RLPData),
                    NodeId = nodeRlp[3].RLPData
                });
            }
            return msg;
        }

        private static byte[] IntToRlp(int value)
        {
            if (value == 0) return new byte[0];
            return ((long)value).ToBytesForRLPEncoding();
        }

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return new byte[0];
            return value.ToBytesForRLPEncoding();
        }

        private static byte[] PortToRlp(ushort port)
        {
            if (port == 0) return new byte[0];
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
