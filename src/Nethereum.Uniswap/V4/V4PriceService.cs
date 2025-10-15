using Nethereum.Uniswap.V4.V4Quoter;
using Nethereum.Uniswap.V4.V4Quoter.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4
{
    public class TokenPrice
    {
        public string Token { get; set; }
        public string QuoteToken { get; set; }
        public decimal Price { get; set; }
        public string QuoteTokenSymbol { get; set; }
        public int Fee { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class V4PriceService
    {
        public static readonly Dictionary<string, string> CommonStablecoins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "USDC", "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48" },
            { "USDT", "0xdAC17F958D2ee523a2206206994597C13D831ec7" },
            { "DAI", "0x6B175474E89094C44Da98b954EedeAC495271d0F" }
        };

        public static readonly int[] CommonFees = new int[] { 500, 3000, 10000 };

        public static async Task<TokenPrice> GetPriceInStablecoinAsync(
            IWeb3 web3,
            string quoterAddress,
            string tokenAddress,
            string stablecoinAddress,
            int tokenDecimals = 18,
            int stablecoinDecimals = 6,
            int fee = 3000,
            int tickSpacing = 60)
        {
            try
            {
                var quoter = new V4QuoterService(web3, quoterAddress);

                var amountIn = BigInteger.Pow(10, tokenDecimals);

                var poolKey = V4PoolKeyHelper.CreateNormalizedForQuoter(tokenAddress, stablecoinAddress, fee, tickSpacing);


                var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(
                    new List<PoolKey> { poolKey },
                    tokenAddress);

                var quoteParams = new QuoteExactParams
                {
                    Path = pathKeys,
                    ExactAmount = amountIn,
                    ExactCurrency = tokenAddress
                };

                var quote = await quoter.QuoteExactInputQueryAsync(quoteParams);

                if (quote.AmountOut == 0)
                {
                    return new TokenPrice
                    {
                        Token = tokenAddress,
                        QuoteToken = stablecoinAddress,
                        Price = 0,
                        Fee = fee,
                        IsValid = false,
                        ErrorMessage = "Pool returned zero output"
                    };
                }

                var price = CalculatePrice(amountIn, quote.AmountOut, tokenDecimals, stablecoinDecimals);

                return new TokenPrice
                {
                    Token = tokenAddress,
                    QuoteToken = stablecoinAddress,
                    Price = price,
                    Fee = fee,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                return new TokenPrice
                {
                    Token = tokenAddress,
                    QuoteToken = stablecoinAddress,
                    Price = 0,
                    Fee = fee,
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public static async Task<TokenPrice> GetBestPriceInStablecoinAsync(
            IWeb3 web3,
            string quoterAddress,
            string tokenAddress,
            string stablecoinAddress,
            int tokenDecimals = 18,
            int stablecoinDecimals = 6,
            int tickSpacing = 60)
        {
            TokenPrice bestPrice = null;

            foreach (var fee in CommonFees)
            {
                var price = await GetPriceInStablecoinAsync(
                    web3,
                    quoterAddress,
                    tokenAddress,
                    stablecoinAddress,
                    tokenDecimals,
                    stablecoinDecimals,
                    fee,
                    tickSpacing);

                bestPrice = SelectBetterPrice(bestPrice, price);
            }

            return bestPrice ?? new TokenPrice
            {
                Token = tokenAddress,
                QuoteToken = stablecoinAddress,
                Price = 0,
                IsValid = false,
                ErrorMessage = "No valid pool found for any fee tier"
            };
        }

        public static async Task<Dictionary<string, TokenPrice>> GetPricesInAllStablecoinsAsync(
            IWeb3 web3,
            string quoterAddress,
            string tokenAddress,
            int tokenDecimals = 18,
            int tickSpacing = 60)
        {
            var prices = new Dictionary<string, TokenPrice>();

            foreach (var stablecoin in CommonStablecoins)
            {
                var stablecoinDecimals = stablecoin.Key == "DAI" ? 18 : 6;

                var price = await GetBestPriceInStablecoinAsync(
                    web3,
                    quoterAddress,
                    tokenAddress,
                    stablecoin.Value,
                    tokenDecimals,
                    stablecoinDecimals,
                    tickSpacing);

                price.QuoteTokenSymbol = stablecoin.Key;
                prices[stablecoin.Key] = price;
            }

            return prices;
        }

        internal static TokenPrice SelectBetterPrice(TokenPrice currentBest, TokenPrice candidate)
        {
            if (candidate == null || !candidate.IsValid)
            {
                return currentBest;
            }

            if (currentBest == null || candidate.Price > currentBest.Price)
            {
                return candidate;
            }

            return currentBest;
        }

        private static decimal SafeFromWei(BigInteger amount, int decimals)
        {
            try
            {
                return UnitConversion.Convert.FromWei(amount, decimals);
            }
            catch (OverflowException)
            {
                return amount.Sign >= 0 ? decimal.MaxValue : decimal.MinValue;
            }
        }

        public static async Task<decimal> GetEthPriceInUsdAsync(
            IWeb3 web3,
            string quoterAddress,
            int tickSpacing = 60)
        {
            var ethAddress = AddressUtil.ZERO_ADDRESS;
            var usdcAddress = CommonStablecoins["USDC"];

            var price = await GetBestPriceInStablecoinAsync(
                web3,
                quoterAddress,
                ethAddress,
                usdcAddress,
                18,
                6,
                tickSpacing);

            return price.IsValid ? price.Price : 0m;
        }

        private static decimal CalculatePrice(
            BigInteger amountIn,
            BigInteger amountOut,
            int decimalsIn,
            int decimalsOut)
        {
            var amountInDecimal = SafeFromWei(amountIn, decimalsIn);
            var amountOutDecimal = SafeFromWei(amountOut, decimalsOut);

            if (amountInDecimal == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountIn), "Amount must be positive");
            }

            return amountOutDecimal / amountInDecimal;
        }
    }
}




