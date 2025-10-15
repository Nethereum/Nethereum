using Nethereum.Util;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V3.Contracts.UniswapV3Pool
{
    public class PoolPairPrice
    {
        public string PoolAddress { get; set; }
        public string Token0 { get; set; }
        public string Token1 { get; set; }
        public decimal PriceToken0InToken1 { get; set; }
        public decimal PriceToken1InToken0 { get; set; }

    }
    public partial class UniswapV3PoolService
    {

        public async Task<PoolPairPrice> GetPoolPricesUsingSlot0Async()
        {
            var token0 = await Token0QueryAsync();
            var token1 = await Token1QueryAsync();
            return await GetPoolPricesUsingSlot0Async(token0, token1);
        }

        public async Task<PoolPairPrice> GetPoolPricesUsingSlot0Async(string token0, string token1)
        {
            int decimals0 = await Web3.Eth.ERC20.GetContractService(token0).DecimalsQueryAsync();
            int decimals1 = await Web3.Eth.ERC20.GetContractService(token1).DecimalsQueryAsync();
            return await GetPoolPricesUsingSlot0Async(token0, token1, decimals0, decimals1);
        }


        public async Task<PoolPairPrice> GetPoolPricesUsingSlot0Async(string token0, string token1, int decimals0, int decimals1)
        {
            var slot0 = await Slot0QueryAsync();

            BigInteger sqrtPriceX96 = slot0.SqrtPriceX96;
            // Compute price (Token0 in terms of Token1)
            
            var sqrtRatio = sqrtPriceX96 / new BigDecimal(BigInteger.Pow(2, 96));
            var decimalFactor = BigInteger.Pow(10, decimals1) / new BigDecimal(BigInteger.Pow(10, decimals0));
            var priceToken0InToken1 = (sqrtRatio * sqrtRatio) /decimalFactor;

            // Compute reverse price (Token1 in terms of Token0)
            var priceToken1InToken0 = 1 / priceToken0InToken1;

            return new PoolPairPrice
            {
                PoolAddress = ContractHandler.ContractAddress,
                Token0 = token0,
                Token1 = token1,
                PriceToken0InToken1 = (decimal)priceToken0InToken1,
                PriceToken1InToken0 = (decimal)priceToken1InToken0
            };
        }

    }
        
}
