using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Events;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.ERC20.Refresh;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20
{
    public interface IErc20TokenService
    {
        ITokenDiscoveryEngine DiscoveryEngine { get; }
        IBatchPriceService BatchPriceService { get; }
        ITokenRefreshOrchestrator RefreshOrchestrator { get; }

        Task<List<TokenInfo>> GetTokenListAsync(long chainId);

        Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress);

        Task<List<TokenBalance>> GetAllBalancesAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            bool includeNative = true,
            NativeTokenConfig nativeToken = null);

        Task<List<TokenBalance>> GetBalancesWithPricesAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            string vsCurrency = "usd",
            bool includeNative = true,
            NativeTokenConfig nativeToken = null);

        Task<List<TokenBalance>> GetBalancesForTokensAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens,
            bool includeNative = false,
            NativeTokenConfig nativeToken = null);

        Task<List<TokenBalance>> GetBalancesForTokensWithPricesAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens,
            string vsCurrency = "usd",
            bool includeNative = false,
            NativeTokenConfig nativeToken = null);

        Task<TokenEventScanResult> ScanTransferEventsAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default);

        Task<List<TokenBalance>> RefreshBalancesFromEventsAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            BigInteger fromBlock,
            IEnumerable<TokenInfo> existingTokens = null,
            string vsCurrency = "usd",
            CancellationToken cancellationToken = default);

        Task InitializeCacheAsync(IEnumerable<long> chainIds);

        Task<bool> SupportsChainAsync(long chainId);

        Task<Dictionary<string, TokenPrice>> GetPricesForTokensAsync(
            long chainId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd");

        Task<TokenPrice> GetNativeTokenPriceAsync(long chainId, string vsCurrency = "usd");
    }
}
