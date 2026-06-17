using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    /// <summary>
    /// CIDR allow-list of IP ranges that peers must fall inside to be accepted
    /// (inbound) or dialed (outbound). An empty list means "no restriction" —
    /// the default behaviour — so this type is opt-in: an operator with no
    /// <see cref="NetRestrict"/> entries sees the same admission decisions as
    /// before.
    /// <para/>
    /// AppChain operators with a private VPC mesh (sequencer + N followers)
    /// configure the VPC's allocation block and reject all other inbound
    /// connections at the TCP-accept boundary — before any handshake CPU is
    /// spent. The same list gates outbound dials so a poisoned discv4/discv5
    /// table cannot make the node connect outside the VPC.
    /// <para/>
    /// Both IPv4 and IPv6 CIDR are accepted (e.g. <c>10.0.0.0/8</c>,
    /// <c>192.168.1.0/24</c>, <c>2001:db8::/32</c>). IPv4-mapped IPv6 addresses
    /// (<c>::ffff:a.b.c.d</c>) are matched against IPv4 entries by unwrapping
    /// to the embedded v4 address before the lookup.
    /// </summary>
    public sealed class NetRestrict
    {
        private readonly List<IPNetwork> _networks = new List<IPNetwork>();

        /// <summary>Number of CIDR entries currently in the list.</summary>
        public int Count => _networks.Count;

        /// <summary>
        /// Parse <paramref name="cidr"/> and add it to the list.
        /// </summary>
        /// <param name="cidr">A CIDR block such as <c>10.0.0.0/8</c> or
        /// <c>2001:db8::/32</c>. Bare IP addresses (e.g. <c>10.0.0.1</c>) are
        /// accepted as <c>/32</c> (v4) or <c>/128</c> (v6) single-host entries.</param>
        /// <exception cref="ArgumentException">Thrown when the value is null,
        /// whitespace, or fails to parse as a CIDR or bare IP.</exception>
        public void Add(string cidr)
        {
            if (string.IsNullOrWhiteSpace(cidr))
                throw new ArgumentException("CIDR cannot be null or whitespace.", nameof(cidr));

            var trimmed = cidr.Trim();
            if (IPNetwork.TryParse(trimmed, out var parsed))
            {
                _networks.Add(parsed);
                return;
            }

            if (IPAddress.TryParse(trimmed, out var bare))
            {
                int prefix = bare.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
                _networks.Add(new IPNetwork(bare, prefix));
                return;
            }

            throw new ArgumentException(
                $"Invalid CIDR or IP address: '{cidr}'.", nameof(cidr));
        }

        /// <summary>
        /// True if <paramref name="ip"/> is covered by at least one CIDR in
        /// the list, or the list is empty ("no restriction"). Null is
        /// conservatively rejected when the list is non-empty so a caller that
        /// failed to resolve a remote endpoint does not accidentally bypass
        /// the gate.
        /// </summary>
        public bool Contains(IPAddress ip)
        {
            if (_networks.Count == 0) return true;
            if (ip == null) return false;

            var lookup = ip;
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
                lookup = ip.MapToIPv4();

            foreach (var net in _networks)
            {
                if (net.BaseAddress.AddressFamily != lookup.AddressFamily) continue;
                if (net.Contains(lookup)) return true;
            }
            return false;
        }
    }
}
