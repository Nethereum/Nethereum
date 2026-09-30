using System.Collections.Generic;
using System.Numerics;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.Blazor.Demo.Services
{
    public class DemoNetworkIconProvider : INetworkIconProvider
    {
        private static readonly Dictionary<BigInteger, string> _networkIcons = new()
        {
            { new BigInteger(1), "/images/networks/1.svg" },
            { new BigInteger(11155111), "/images/networks/1.svg" },
            
            { new BigInteger(137), "/images/networks/137.svg" },
            { new BigInteger(80001), "/images/networks/137.svg" },
            
            { new BigInteger(56), "/images/networks/56.svg" },
            { new BigInteger(97), "/images/networks/56.svg" },
            
            { new BigInteger(42161), "/images/networks/42161.svg" },
            { new BigInteger(421614), "/images/networks/42161.svg" },
            
            { new BigInteger(10), "/images/networks/10.svg" },
            { new BigInteger(11155420), "/images/networks/10.svg" },
            
            { new BigInteger(8453), "/images/networks/8453.svg" },
            { new BigInteger(84532), "/images/networks/8453.svg" },
            
            { new BigInteger(324), "/images/networks/324.svg" },
            { new BigInteger(300), "/images/networks/324.svg" },
            
            { new BigInteger(43114), "/images/networks/43114.svg" },
            
            { new BigInteger(59144), "/images/networks/59144.svg" },
            { new BigInteger(59140), "/images/networks/59144.svg" },
            
            { new BigInteger(100), "/images/networks/100.svg" },
            
            { new BigInteger(42220), "/images/networks/42220.svg" },
            
            { new BigInteger(534352), "/images/networks/534352.svg" },
            
            { new BigInteger(7777777), "/images/networks/7777777.svg" },
            
            { new BigInteger(5000), "/images/networks/5000.svg" },
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