using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.ChainNode.Hosting.Rpc
{
    public sealed class RpcOriginPolicy
    {
        public const string Wildcard = "*";

        private readonly List<string> _origins;
        private readonly bool _allowAll;

        public RpcOriginPolicy(IEnumerable<string>? allowedOrigins)
        {
            _origins = (allowedOrigins ?? Enumerable.Empty<string>())
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().ToLowerInvariant())
                .ToList();
            _allowAll = _origins.Contains(Wildcard);
        }

        public bool IsAllowed(string? originHeader)
        {
            if (originHeader == null) return true;
            if (_allowAll) return true;

            var browser = Parse(originHeader.ToLowerInvariant());
            if (browser == null) return false;

            foreach (var rule in _origins)
            {
                var allowed = Parse(rule);
                if (allowed == null) continue;
                if (allowed.Value.Scheme.Length > 0 && allowed.Value.Scheme != browser.Value.Scheme) continue;
                if (allowed.Value.Host.Length > 0 && allowed.Value.Host != browser.Value.Host) continue;
                if (allowed.Value.Port.Length > 0 && allowed.Value.Port != browser.Value.Port) continue;
                return true;
            }

            return false;
        }

        private static (string Scheme, string Host, string Port)? Parse(string origin)
        {
            if (origin.Contains("://"))
            {
                return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    ? (uri.Scheme, uri.Host, uri.IsDefaultPort ? string.Empty : uri.Port.ToString())
                    : ((string, string, string)?)null;
            }

            var colon = origin.LastIndexOf(':');
            return colon > 0 && colon == origin.IndexOf(':')
                ? (string.Empty, origin.Substring(0, colon), origin.Substring(colon + 1))
                : (string.Empty, origin, string.Empty);
        }
    }
}
