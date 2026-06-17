using System.Net;
using Nethereum.RLP;

namespace Nethereum.DevP2P.Discv4
{
    /// <summary>
    /// discv4 endpoint: [ip, udp_port, tcp_port]
    /// IP is IPv4 (4 bytes) or IPv6 (16 bytes). udp_port is required;
    /// tcp_port may be 0 if the node only does discovery.
    /// </summary>
    public class Discv4Endpoint
    {
        public IPAddress IP { get; set; } = IPAddress.Loopback;
        public ushort UdpPort { get; set; }
        public ushort TcpPort { get; set; }

        public byte[] Encode()
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(IP.GetAddressBytes()),
                RLP.RLP.EncodeElement(BigEndian(UdpPort)),
                RLP.RLP.EncodeElement(BigEndian(TcpPort))
            );
        }

        public static Discv4Endpoint Decode(RLPCollection list)
        {
            return new Discv4Endpoint
            {
                IP = new IPAddress(list[0].RLPData ?? new byte[4]),
                UdpPort = ReadPort(list[1].RLPData),
                TcpPort = ReadPort(list[2].RLPData)
            };
        }

        private static byte[] BigEndian(ushort value)
        {
            if (value == 0) return new byte[0];
            return new[] { (byte)((value >> 8) & 0xff), (byte)(value & 0xff) };
        }

        private static ushort ReadPort(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            if (data.Length == 1) return data[0];
            return (ushort)((data[0] << 8) | data[1]);
        }
    }
}
