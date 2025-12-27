using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public static class BridgeTokenFilter
    {
        public static bool IsBridgeToken(TokenInfo token)
        {
            if (token == null) return false;

            var name = token.Name?.ToLowerInvariant() ?? "";
            var symbol = token.Symbol?.ToUpperInvariant() ?? "";

            if (name.Contains("bridged")) return true;

            if (name.Contains(" bridge") || name.StartsWith("bridge")) return true;

            if (symbol.StartsWith("AXL-")) return true;

            if (name.StartsWith("any")) return true;

            if (name.Contains("wormhole")) return true;

            return false;
        }

        public static bool IsBridgeToken(string name, string symbol)
        {
            var nameLower = name?.ToLowerInvariant() ?? "";
            var symbolUpper = symbol?.ToUpperInvariant() ?? "";

            if (nameLower.Contains("bridged")) return true;

            if (nameLower.Contains(" bridge") || nameLower.StartsWith("bridge")) return true;

            if (symbolUpper.StartsWith("AXL-")) return true;

            if (nameLower.StartsWith("any")) return true;

            if (nameLower.Contains("wormhole")) return true;

            return false;
        }

        public static List<TokenInfo> FilterBridgeTokens(IEnumerable<TokenInfo> tokens)
        {
            if (tokens == null) return new List<TokenInfo>();
            return tokens.Where(t => !IsBridgeToken(t)).ToList();
        }

        public static int CountBridgeTokens(IEnumerable<TokenInfo> tokens)
        {
            if (tokens == null) return 0;
            return tokens.Count(IsBridgeToken);
        }
    }
}
