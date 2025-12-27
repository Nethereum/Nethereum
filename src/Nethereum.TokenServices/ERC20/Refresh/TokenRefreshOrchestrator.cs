using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Events;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Refresh
{
    public class TokenRefreshOrchestrator : ITokenRefreshOrchestrator
    {
        private readonly ITokenEventScanner _eventScanner;
        private readonly ITokenBalanceProvider _balanceProvider;
        private readonly ITokenListProvider _tokenListProvider;

        public TokenRefreshOrchestrator(
            ITokenEventScanner eventScanner,
            ITokenBalanceProvider balanceProvider,
            ITokenListProvider tokenListProvider = null)
        {
            _eventScanner = eventScanner ?? throw new ArgumentNullException(nameof(eventScanner));
            _balanceProvider = balanceProvider ?? throw new ArgumentNullException(nameof(balanceProvider));
            _tokenListProvider = tokenListProvider;
        }

        public async Task<TokenRefreshResult> RefreshAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            RefreshOptions options,
            CancellationToken cancellationToken = default)
        {
            options ??= new RefreshOptions();

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var toBlock = options.ToBlock ?? currentBlock.Value;
            var fromBlock = options.FromBlock > options.ReorgSafetyBuffer
                ? options.FromBlock - options.ReorgSafetyBuffer
                : options.FromBlock;

            if (fromBlock >= toBlock)
            {
                return TokenRefreshResult.NoChanges(fromBlock, toBlock);
            }

            var result = new TokenRefreshResult
            {
                FromBlock = fromBlock,
                ToBlock = toBlock
            };

            try
            {
                var eventResult = await _eventScanner.ScanTransferEventsAsync(
                    web3, accountAddress, fromBlock, toBlock, cancellationToken);

                if (!eventResult.Success)
                {
                    result.Success = false;
                    result.EventScanSuccess = false;
                    result.EventScanError = eventResult.ErrorMessage;
                    return result;
                }

                result.EventScanSuccess = true;
                result.AffectedTokenAddresses = eventResult.AffectedTokenAddresses;

                if (eventResult.AffectedTokenAddresses.Any())
                {
                    var tokensToRefresh = await BuildTokenInfoListAsync(
                        chainId, eventResult.AffectedTokenAddresses);

                    var balances = await _balanceProvider.GetBalancesAsync(web3, accountAddress, tokensToRefresh);
                    result.UpdatedBalances = balances;
                    result.TokensUpdated = balances.Count;
                    result.NewTokensFound = balances.Count(b => b.Balance > 0);
                }

                if (options.IncludeNativeToken)
                {
                    try
                    {
                        var nativeConfig = NativeTokenConfig.ForChain(chainId);
                        result.NativeBalance = await _balanceProvider.GetNativeBalanceAsync(web3, accountAddress, nativeConfig);
                    }
                    catch
                    {
                        // Native balance fetch is non-critical
                    }
                }

                result.Success = true;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.EventScanError = "Operation cancelled";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.EventScanSuccess = false;
                result.EventScanError = ex.Message;
            }

            return result;
        }

        public async Task<TokenRefreshResult> RefreshMultipleAccountsAsync(
            IWeb3 web3,
            IEnumerable<string> accountAddresses,
            long chainId,
            RefreshOptions options,
            CancellationToken cancellationToken = default)
        {
            var accounts = accountAddresses?.ToList() ?? new List<string>();
            if (!accounts.Any())
            {
                return TokenRefreshResult.NoChanges(options?.FromBlock ?? 0, 0);
            }

            options ??= new RefreshOptions();
            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var toBlock = options.ToBlock ?? currentBlock.Value;

            var combinedResult = new TokenRefreshResult
            {
                FromBlock = options.FromBlock,
                ToBlock = toBlock,
                Success = true,
                EventScanSuccess = true
            };

            var allAffectedAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allBalances = new List<TokenBalance>();

            foreach (var account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var accountOptions = new RefreshOptions
                {
                    FromBlock = options.FromBlock,
                    ToBlock = toBlock,
                    ReorgSafetyBuffer = options.ReorgSafetyBuffer,
                    IncludeNativeToken = options.IncludeNativeToken
                };

                var accountResult = await RefreshAsync(web3, account, chainId, accountOptions, cancellationToken);

                if (!accountResult.EventScanSuccess)
                {
                    combinedResult.EventScanSuccess = false;
                    combinedResult.EventScanError = accountResult.EventScanError;
                }

                foreach (var addr in accountResult.AffectedTokenAddresses)
                {
                    allAffectedAddresses.Add(addr);
                }

                allBalances.AddRange(accountResult.UpdatedBalances);
                combinedResult.TokensUpdated += accountResult.TokensUpdated;
                combinedResult.NewTokensFound += accountResult.NewTokensFound;
            }

            combinedResult.AffectedTokenAddresses = allAffectedAddresses.ToList();
            combinedResult.UpdatedBalances = allBalances;

            return combinedResult;
        }

        private async Task<List<TokenInfo>> BuildTokenInfoListAsync(long chainId, List<string> addresses)
        {
            var result = new List<TokenInfo>();

            foreach (var address in addresses)
            {
                TokenInfo tokenInfo = null;

                if (_tokenListProvider != null)
                {
                    tokenInfo = await _tokenListProvider.GetTokenAsync(chainId, address);
                }

                result.Add(tokenInfo ?? new TokenInfo
                {
                    Address = address,
                    ChainId = chainId,
                    Symbol = "???",
                    Name = "Unknown Token",
                    Decimals = 18
                });
            }

            return result;
        }
    }
}
