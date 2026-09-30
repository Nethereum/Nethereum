using System.Net;
using Nethereum.DevP2P.Netutil;
using Nethereum.RLP;

namespace Nethereum.DevP2P.Discv4
{
    public class Discv4Endpoint
    {
        public IPAddress IP { get; set; } = IPAddress.Loopback;
        public ushort UdpPort { get; set; }
        public ushort TcpPort { get; set; }

        public byte[] Encode()
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(IP.GetAddressBytes()),
                RLP.RLP.EncodeElement(PortCodec.EncodeBigEndian(UdpPort)),
                RLP.RLP.EncodeElement(PortCodec.EncodeBigEndian(TcpPort))
            );
        }

        public static Discv4Endpoint Decode(RLPCollection list)
        {
            return new Discv4Endpoint
            {
                IP = new IPAddress(list[0].RLPData ?? new byte[4]),
                UdpPort = PortCodec.DecodeBigEndian(list[1].RLPData),
                TcpPort = PortCodec.DecodeBigEndian(list[2].RLPData)
            };
        }
    }
}
