using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    /// <summary>
    /// Per-subnet admission counter that bounds how many peers from the same
    /// IPv4 /<see cref="IPv4Prefix"/> (or IPv6 /<see cref="IPv6Prefix"/>) may
    /// simultaneously occupy the pool. Eclipse-attack defence: a /24 owner
    /// otherwise fills the dial budget × subnet-size before any per-IP gate
    /// trips. Mirrors the canonical distinct-subnet set (bucketSubnet=24,
    /// tableSubnet=24).
    /// <para/>
    /// Thread-safe. <see cref="TryAdd"/> increments the count for the
    /// containing subnet only when the cap has not been reached; the caller
    /// must call <see cref="Remove"/> on disconnect to free the slot. IPv4 and
    /// IPv6 are tracked independently; the family discriminator is folded
    /// into the key so a v4 and v6 subnet with colliding prefix bytes cannot
    /// share a slot.
    /// </summary>
    public sealed class SubnetTracker
    {
        /// <summary>IPv4 prefix length (e.g. 24 = /24). 0 disables the v4 cap.</summary>
        public int IPv4Prefix { get; }

        /// <summary>IPv6 prefix length (e.g. 64 = /64). 0 disables the v6 cap.</summary>
        public int IPv6Prefix { get; }

        /// <summary>Maximum entries allowed per IPv4 subnet. 0 disables the v4 cap.</summary>
        public int MaxPerIPv4Subnet { get; }

        /// <summary>Maximum entries allowed per IPv6 subnet. 0 disables the v6 cap.</summary>
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

        /// <summary>
        /// Returns true if <paramref name="address"/> can be admitted (and
        /// records the admission), false if the containing subnet is full.
        /// Null addresses, loopback, and family-disabled caps are always
        /// admitted without affecting any count.
        /// </summary>
        public bool TryAdd(IPAddress address)
        {
            var key = GetSubnetKey(address, out int cap);
            if (key == null || cap <= 0) return true;

            bool admitted = false;
            _counts.AddOrUpdate(
                key,
                _ => { admitted = true; return 1; },
                (_, existing) =>
                {
                    if (existing >= cap) return existing;
                    admitted = true;
                    return existing + 1;
                });
            return admitted;
        }

        /// <summary>
        /// Decrement the count for <paramref name="address"/>'s subnet. No-op
        /// when the address is null, loopback, or its family has no cap.
        /// Decrement-or-clear CAS so two concurrent removes cannot drive the
        /// stored count negative.
        /// </summary>
        public void Remove(IPAddress address)
        {
            var key = GetSubnetKey(address, out int cap);
            if (key == null || cap <= 0) return;

            while (_counts.TryGetValue(key, out var n))
            {
                if (n <= 1)
                {
                    var kvp = new KeyValuePair<string, int>(key, n);
                    if (((ICollection<KeyValuePair<string, int>>)_counts).Remove(kvp)) return;
                }
                else if (_counts.TryUpdate(key, n - 1, n))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Current admitted count for <paramref name="address"/>'s subnet.
        /// Returns 0 for null, loopback, family-disabled, or never-seen.
        /// Test/inspection helper — admission decisions go through
        /// <see cref="TryAdd"/>.
        /// </summary>
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

            var lookup = address;
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
                lookup = address.MapToIPv4();

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
