using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Nethereum.DevP2P.Netutil
{
    public sealed class NetRestrict
    {
        private readonly List<IPNetwork> _networks = new List<IPNetwork>();

        public int Count => _networks.Count;

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

        public bool Contains(IPAddress ip)
        {
            if (_networks.Count == 0) return true;
            if (ip == null) return false;

            var lookup = ip.NormalizeMappedV4();

            foreach (var net in _networks)
            {
                if (net.BaseAddress.AddressFamily != lookup.AddressFamily) continue;
                if (net.Contains(lookup)) return true;
            }
            return false;
        }
    }
}
