using Nethereum.Uniswap.V4.Pricing;
using Nethereum.Uniswap.V4.Utils;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Positions
{

    public  class PositionInfoUtils
    {
        public static PositionInfoUtils Current { get; } = new PositionInfoUtils();
        public  PositionInfo CreatePositionInfo(
            BigInteger tokenId,
            byte[] poolId,
            string currency0,
            string currency1,
            uint fee,
            int tickSpacing,
            string hooks,
            int tickLower,
            int tickUpper,
            BigInteger liquidity,
            BigInteger sqrtPriceX96)
        {
            var amounts = LiquidityCalculator.Current.GetAmountsForLiquidityByTicks(sqrtPriceX96, tickLower, tickUpper, liquidity);

            return new PositionInfo
            {
                TokenId = tokenId,
                PoolId = poolId,
                Currency0 = currency0,
                Currency1 = currency1,
                Fee = fee,
                TickSpacing = tickSpacing,
                Hooks = hooks,
                TickLower = tickLower,
                TickUpper = tickUpper,
                Liquidity = liquidity,
                Amount0 = amounts.Amount0,
                Amount1 = amounts.Amount1,
                PriceAtTickLower = V4TickMath.Current.GetPriceAtTick(tickLower),
                PriceAtTickUpper = V4TickMath.Current.GetPriceAtTick(tickUpper),
                CurrentPrice = PriceCalculator.Current.CalculatePriceFromSqrtPriceX96(sqrtPriceX96)
            };
        }

        public  bool IsPositionInRange(int currentTick, int tickLower, int tickUpper)
        {
            return currentTick >= tickLower && currentTick < tickUpper;
        }
    }
}





