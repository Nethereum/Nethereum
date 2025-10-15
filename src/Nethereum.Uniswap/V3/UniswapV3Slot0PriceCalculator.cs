using System;
using System.Threading.Tasks;
using Nethereum.Uniswap.V3.Contracts.UniswapV3Pool;
using Nethereum.Uniswap.V3.UniswapV3Factory;

namespace Nethereum.Uniswap.V3
{
    public class UniswapV3Slot0PriceCalculator
    {
        private readonly Web3.Web3 _web3;
        private readonly string _factoryAddress;

        public UniswapV3Slot0PriceCalculator(Web3.Web3 web3, string factoryAddress)
        {
            _web3 = web3;
            _factoryAddress = factoryAddress;
        }

        public async Task<PoolPairPrice> GetPoolPricesAsync(string token0, string token1, uint fee)
        {
            var factoryService = new UniswapV3FactoryService(_web3, _factoryAddress);
            var poolAddress = await  factoryService.GetPoolQueryAsync(token0, token1, fee);

            if (string.IsNullOrEmpty(poolAddress) || poolAddress == "0x0000000000000000000000000000000000000000")
            {
                throw new Exception("Pool does not exist for the given token pair and fee.");
            }

            var poolService = new UniswapV3PoolService(_web3, poolAddress);
            return await poolService.GetPoolPricesUsingSlot0Async(token0, token1);
        }
    }
}
