using System.Collections.Generic;
using System.Numerics;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.Avalonia.Demo.Services
{
    public class DemoNetworkIconProvider : INetworkIconProvider
    {
        private static readonly Dictionary<BigInteger, string> _networkIcons = new()
        {
            { new BigInteger(1), "/Assets/networks/1.svg" },
            { new BigInteger(11155111), "/Assets/networks/1.svg" },

            { new BigInteger(137), "/Assets/networks/137.svg" },
            { new BigInteger(80001), "/Assets/networks/137.svg" },

            { new BigInteger(56), "/Assets/networks/56.svg" },
            { new BigInteger(97), "/Assets/networks/56.svg" },

            { new BigInteger(42161), "/Assets/networks/42161.svg" },
            { new BigInteger(421614), "/Assets/networks/42161.svg" },

            { new BigInteger(10), "/Assets/networks/10.svg" },
            { new BigInteger(11155420), "/Assets/networks/10.svg" },

            { new BigInteger(8453), "/Assets/networks/8453.svg" },
            { new BigInteger(84532), "/Assets/networks/8453.svg" },

            { new BigInteger(324), "/Assets/networks/324.svg" },
            { new BigInteger(300), "/Assets/networks/324.svg" },

            { new BigInteger(43114), "/Assets/networks/43114.svg" },

            { new BigInteger(59144), "/Assets/networks/59144.svg" },
            { new BigInteger(59140), "/Assets/networks/59144.svg" },

            { new BigInteger(100), "/Assets/networks/100.svg" },

            { new BigInteger(42220), "/Assets/networks/42220.svg" },

            { new BigInteger(534352), "/Assets/networks/534352.svg" },

            { new BigInteger(7777777), "/Assets/networks/7777777.svg" },

            { new BigInteger(5000), "/Assets/networks/5000.svg" },
        };

        public string? GetNetworkIcon(BigInteger chainId)
        {
            return _networkIcons.TryGetValue(chainId, out var iconUrl) ? iconUrl : null;
        }

        public bool HasNetworkIcon(BigInteger chainId)
        {
            return _networkIcons.ContainsKey(chainId);
        }
    }
}