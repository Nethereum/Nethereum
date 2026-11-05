using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    public static class IpAddressExtensions
    {
        private static readonly IPNetwork[] SpecialNetworks = ParseSpecialNetworks();

        private static IPNetwork[] ParseSpecialNetworks()
        {
            var cidrs = new[]
            {
                "0.0.0.0/8", "192.0.0.0/29", "192.0.0.9/32", "192.0.0.170/32", "192.0.0.171/32",
                "192.0.2.0/24", "192.31.196.0/24", "192.52.193.0/24", "192.88.99.0/24", "192.175.48.0/24",
                "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "255.255.255.255/32",
                "100::/64", "2001::/32", "2001:1::1/128", "2001:2::/48", "2001:3::/32", "2001:4:112::/48",
                "2001:5::/32", "2001:10::/28", "2001:20::/28", "2001:db8::/32", "2002::/16"
            };
            var networks = new IPNetwork[cidrs.Length];
            for (var i = 0; i < cidrs.Length; i++) networks[i] = IPNetwork.Parse(cidrs[i]);
            return networks;
        }

        public static IPAddress NormalizeMappedV4(this IPAddress ip)
            => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6
                ? ip.MapToIPv4()
                : ip;

        public static bool IsLan(this IPAddress ip)
        {
            var addr = ip.NormalizeMappedV4();
            if (IPAddress.IsLoopback(addr)) return true;
            var b = addr.GetAddressBytes();
            if (b.Length == 4)
                return b[0] == 10
                    || (b[0] == 172 && (b[1] & 0xF0) == 16)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 169 && b[1] == 254);
            if (b.Length == 16)
                return (b[0] & 0xFE) == 0xFC
                    || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80);
            return false;
        }

        public static bool IsSpecialNetwork(this IPAddress ip)
        {
            var addr = ip.NormalizeMappedV4();
            var b = addr.GetAddressBytes();
            if (b.Length == 4 && (b[0] & 0xF0) == 0xE0) return true;
            if (b.Length == 16 && b[0] == 0xFF) return true;
            foreach (var net in SpecialNetworks)
            {
                if (net.BaseAddress.AddressFamily == addr.AddressFamily && net.Contains(addr)) return true;
            }
            return false;
        }

        public static bool IsValidRelayTarget(this IPAddress addr, IPAddress sender)
        {
            if (addr == null || sender == null) return false;
            var target = addr.NormalizeMappedV4();
            if (target.Equals(IPAddress.Any) || target.Equals(IPAddress.IPv6Any)) return false;
            if (target.IsSpecialNetwork()) return false;
            if (IPAddress.IsLoopback(target) && !IPAddress.IsLoopback(sender.NormalizeMappedV4())) return false;
            if (target.IsLan() && !sender.IsLan()) return false;
            return true;
        }
    }
}
