using System.Numerics;

namespace Nethereum.Wallet.UI.Components.Utils
{
    public interface INetworkIconProvider
    {
        string? GetNetworkIcon(BigInteger chainId);
        bool HasNetworkIcon(BigInteger chainId);
    }
    public class DefaultNetworkIconProvider : INetworkIconProvider
    {
        public string? GetNetworkIcon(BigInteger chainId) => null;
        public bool HasNetworkIcon(BigInteger chainId) => false;
    }
}