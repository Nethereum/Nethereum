using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    public sealed class SubnetTracker
    {
        public int IPv4Prefix { get; }

        public int IPv6Prefix { get; }

        public int MaxPerIPv4Subnet { get; }

        public int MaxPerIPv6Subnet { get; }

        private readonly ConcurrentDictionary<string, int> _counts =
            new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        public SubnetTracker(
            int maxPerIPv4Subnet = 10,
            int ipv4Prefix = 24,
            int maxPerIPv6Subnet = 10,
            int ipv6Prefix = 64)
        {
            if (ipv4Prefix < 0 || ipv4Prefix > 32)
                throw new ArgumentOutOfRangeException(nameof(ipv4Prefix), "IPv4 prefix must be in [0, 32].");
            if (ipv6Prefix < 0 || ipv6Prefix > 128)
                throw new ArgumentOutOfRangeException(nameof(ipv6Prefix), "IPv6 prefix must be in [0, 128].");
            if (maxPerIPv4Subnet < 0) throw new ArgumentOutOfRangeException(nameof(maxPerIPv4Subnet));
            if (maxPerIPv6Subnet < 0) throw new ArgumentOutOfRangeException(nameof(maxPerIPv6Subnet));

            IPv4Prefix = ipv4Prefix;
            IPv6Prefix = ipv6Prefix;
            MaxPerIPv4Subnet = maxPerIPv4Subnet;
            MaxPerIPv6Subnet = maxPerIPv6Subnet;
        }

        public bool TryAdd(IPAddress address)
        {
            var key = GetSubnetKey(address, out int cap);
            if (key == null || cap <= 0) return true;
            return ConcurrentCounter.TryReserve(_counts, key, cap);
        }

        public void Remove(IPAddress address)
        {
            var key = GetSubnetKey(address, out int cap);
            if (key == null || cap <= 0) return;
            ConcurrentCounter.DecrementOrRemove(_counts, key);
        }

        public int Count(IPAddress address)
        {
            var key = GetSubnetKey(address, out int cap);
            if (key == null || cap <= 0) return 0;
            return _counts.TryGetValue(key, out var n) ? n : 0;
        }

        private string? GetSubnetKey(IPAddress? address, out int cap)
        {
            cap = 0;
            if (address == null) return null;

            var lookup = address.NormalizeMappedV4();

            if (IPAddress.IsLoopback(lookup)) return null;

            if (lookup.AddressFamily == AddressFamily.InterNetwork)
            {
                if (IPv4Prefix <= 0 || MaxPerIPv4Subnet <= 0) return null;
                cap = MaxPerIPv4Subnet;
                return BuildKey("4", lookup.GetAddressBytes(), IPv4Prefix);
            }

            if (lookup.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IPv6Prefix <= 0 || MaxPerIPv6Subnet <= 0) return null;
                cap = MaxPerIPv6Subnet;
                return BuildKey("6", lookup.GetAddressBytes(), IPv6Prefix);
            }

            return null;
        }

        private static string BuildKey(string familyTag, byte[] addressBytes, int prefixBits)
        {
            int fullBytes = prefixBits / 8;
            int remainderBits = prefixBits % 8;
            int keyBytes = fullBytes + (remainderBits == 0 ? 0 : 1);

            var sb = new System.Text.StringBuilder(familyTag.Length + 1 + keyBytes * 2);
            sb.Append(familyTag).Append(':');
            for (int i = 0; i < fullBytes; i++) sb.Append(addressBytes[i].ToString("x2"));
            if (remainderBits > 0)
            {
                int mask = 0xFF & (0xFF << (8 - remainderBits));
                sb.Append(((byte)(addressBytes[fullBytes] & mask)).ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
