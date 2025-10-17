using Nethereum.Web3;

namespace Nethereum.Uniswap.V4
{
    /// <summary>
    /// Extension methods for Web3 to access Uniswap V4 services
    /// </summary>
    public static class Web3Extensions
    {
        /// <summary>
        /// Access Uniswap V4 services for Ethereum Mainnet
        /// </summary>
        public static UniswapV4Service UniswapV4(this IWeb3 web3)
        {
            return new UniswapV4Service(web3, UniswapV4Addresses.Mainnet);
        }

        /// <summary>
        /// Access Uniswap V4 services with custom addresses
        /// </summary>
        public static UniswapV4Service UniswapV4(this IWeb3 web3, UniswapV4Addresses addresses)
        {
            return new UniswapV4Service(web3, addresses);
        }
    }
}
