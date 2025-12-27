using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Balances
{
    public interface ITokenBalanceProvider
    {
        Task<List<TokenBalance>> GetBalancesAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens);

        Task<TokenBalance> GetNativeBalanceAsync(
            IWeb3 web3,
            string accountAddress,
            NativeTokenConfig nativeToken);
    }

    public class NativeTokenConfig
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; } = 18;
        public string LogoUri { get; set; }
        public long ChainId { get; set; }
        public bool IsTestnet { get; set; }

        public static NativeTokenConfig Ethereum => new NativeTokenConfig
        {
            Symbol = "ETH",
            Name = "Ethereum",
            Decimals = 18,
            ChainId = 1,
            IsTestnet = false
        };

        public static NativeTokenConfig ForChain(long chainId, string symbol = "ETH", string name = "Native Token", int decimals = 18, bool isTestnet = false)
        {
            return new NativeTokenConfig
            {
                Symbol = symbol,
                Name = name,
                Decimals = decimals,
                ChainId = chainId,
                IsTestnet = isTestnet
            };
        }
    }
}
