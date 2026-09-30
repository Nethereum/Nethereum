using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    public static class IpAddressExtensions
    {
        public static IPAddress NormalizeMappedV4(this IPAddress ip)
            => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6
                ? ip.MapToIPv4()
                : ip;
    }
}
