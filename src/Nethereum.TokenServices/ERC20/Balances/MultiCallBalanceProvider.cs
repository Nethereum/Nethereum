using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Balances
{
    public class MultiCallBalanceProvider : ITokenBalanceProvider
    {
        private readonly int _multicallBatchSize;
        private readonly string _multiCallAddress;
        private const int MinBatchSize = 10;

        public MultiCallBalanceProvider(
            int multicallBatchSize = 100,
            string multiCallAddress = null)
        {
            _multicallBatchSize = multicallBatchSize;
            _multiCallAddress = multiCallAddress;
        }

        public async Task<List<TokenBalance>> GetBalancesAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens)
        {
            var tokensList = tokens?.ToList();
            if (tokensList == null || !tokensList.Any())
            {
                return new List<TokenBalance>();
            }

            var uniqueTokens = tokensList
                .GroupBy(t => t.Address?.ToLowerInvariant())
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => g.First())
                .ToList();

            var contractAddresses = uniqueTokens.Select(t => t.Address).ToList();

            return await GetBalancesWithRetryAsync(web3, accountAddress, uniqueTokens, contractAddresses, _multicallBatchSize);
        }

        private async Task<List<TokenBalance>> GetBalancesWithRetryAsync(
            IWeb3 web3,
            string accountAddress,
            List<TokenInfo> uniqueTokens,
            List<string> contractAddresses,
            int batchSize)
        {
            try
            {
                var erc20Service = web3.Eth.ERC20;
                List<TokenOwnerBalance> ownerBalances;

                if (!string.IsNullOrEmpty(_multiCallAddress))
                {
                    ownerBalances = await erc20Service.GetAllTokenBalancesUsingMultiCallAsync(
                        accountAddress,
                        contractAddresses,
                        batchSize,
                        _multiCallAddress);
                }
                else
                {
                    ownerBalances = await erc20Service.GetAllTokenBalancesUsingMultiCallAsync(
                        accountAddress,
                        contractAddresses,
                        batchSize);
                }

                return MapToTokenBalances(uniqueTokens, ownerBalances, accountAddress);
            }
            catch (Exception ex) when (IsGasOrSizeError(ex) && batchSize > MinBatchSize)
            {
                var smallerBatchSize = batchSize / 2;
                System.Diagnostics.Debug.WriteLine($"[MultiCallBalanceProvider] Gas/size error with batch {batchSize}, retrying with {smallerBatchSize}: {ex.Message}");
                return await GetBalancesWithRetryAsync(web3, accountAddress, uniqueTokens, contractAddresses, smallerBatchSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MultiCallBalanceProvider] Multicall failed for {uniqueTokens.Count} tokens: {ex.Message}");

                return uniqueTokens.Select(t => new TokenBalance
                {
                    Token = t,
                    Balance = BigInteger.Zero,
                    IsNative = false
                }).ToList();
            }
        }

        private static bool IsGasOrSizeError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("out of gas") ||
                   message.Contains("gas required exceeds") ||
                   message.Contains("exceeds block gas limit") ||
                   message.Contains("request entity too large") ||
                   message.Contains("response size exceeded");
        }

        private List<TokenBalance> MapToTokenBalances(
            List<TokenInfo> tokens,
            List<TokenOwnerBalance> ownerBalances,
            string accountAddress)
        {
            var balancesByAddress = ownerBalances
                .Where(b => b.Owner?.ToLowerInvariant() == accountAddress.ToLowerInvariant())
                .ToDictionary(
                    b => b.ContractAddress.ToLowerInvariant(),
                    b => b.Balance);

            return tokens.Select(token =>
            {
                balancesByAddress.TryGetValue(token.Address.ToLowerInvariant(), out var balance);
                return new TokenBalance
                {
                    Token = token,
                    Balance = balance,
                    IsNative = false
                };
            }).ToList();
        }

        public async Task<TokenBalance> GetNativeBalanceAsync(
            IWeb3 web3,
            string accountAddress,
            NativeTokenConfig nativeToken)
        {
            var balance = await web3.Eth.GetBalance.SendRequestAsync(accountAddress);

            var tokenInfo = new TokenInfo
            {
                Address = null,
                Symbol = nativeToken.Symbol,
                Name = nativeToken.Name,
                Decimals = nativeToken.Decimals,
                LogoUri = nativeToken.LogoUri,
                ChainId = nativeToken.ChainId
            };

            return new TokenBalance
            {
                Token = tokenInfo,
                Balance = balance.Value,
                IsNative = true
            };
        }
    }
}
