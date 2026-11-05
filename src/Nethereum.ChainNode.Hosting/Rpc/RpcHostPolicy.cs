using System;
using System.Collections.Generic;
using System.Net;

namespace Nethereum.ChainNode.Hosting.Rpc
{
    public sealed class RpcHostPolicy
    {
        public const string Wildcard = "*";
        public const string Localhost = "localhost";

        private readonly HashSet<string> _hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Localhost };

        public RpcHostPolicy(IEnumerable<string>? allowedHosts, string? bindHost)
        {
            if (allowedHosts != null)
            {
                foreach (var host in allowedHosts)
                {
                    if (!string.IsNullOrWhiteSpace(host)) _hosts.Add(host.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(bindHost)) _hosts.Add(bindHost.Trim());
        }

        public bool IsAllowed(string? hostHeader)
        {
            if (string.IsNullOrEmpty(hostHeader)) return true;

            var host = HostWithoutPort(hostHeader);
            if (IPAddress.TryParse(host, out _)) return true;

            return _hosts.Contains(Wildcard) || _hosts.Contains(host);
        }

        private static string HostWithoutPort(string hostHeader)
        {
            if (hostHeader.StartsWith("[", StringComparison.Ordinal))
            {
                var end = hostHeader.IndexOf(']');
                return end > 0 ? hostHeader.Substring(1, end - 1) : hostHeader;
            }

            var colon = hostHeader.IndexOf(':');
            if (colon >= 0 && colon == hostHeader.LastIndexOf(':')) return hostHeader.Substring(0, colon);
            return hostHeader;
        }
    }
}
