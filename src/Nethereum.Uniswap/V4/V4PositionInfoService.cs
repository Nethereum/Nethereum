using System.Numerics;

namespace Nethereum.Uniswap.V4
{
    public class V4PositionInfo
    {
        public BigInteger TokenId { get; set; }
        public byte[] PoolId { get; set; }
        public string Currency0 { get; set; }
        public string Currency1 { get; set; }
        public uint Fee { get; set; }
        public int TickSpacing { get; set; }
        public string Hooks { get; set; }
        public int TickLower { get; set; }
        public int TickUpper { get; set; }
        public BigInteger Liquidity { get; set; }
        public BigInteger Amount0 { get; set; }
        public BigInteger Amount1 { get; set; }
        public decimal PriceAtTickLower { get; set; }
        public decimal PriceAtTickUpper { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    public static class V4PositionInfoHelper
    {
        public static V4PositionInfo CreatePositionInfo(
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
            var amounts = V4LiquidityMath.GetAmountsForLiquidityByTicks(sqrtPriceX96, tickLower, tickUpper, liquidity);

            return new V4PositionInfo
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
                PriceAtTickLower = V4TickMath.GetPriceAtTick(tickLower),
                PriceAtTickUpper = V4TickMath.GetPriceAtTick(tickUpper),
                CurrentPrice = V4PriceCalculator.CalculatePriceFromSqrtPriceX96(sqrtPriceX96)
            };
        }

        public static bool IsPositionInRange(int currentTick, int tickLower, int tickUpper)
        {
            return currentTick >= tickLower && currentTick < tickUpper;
        }
    }
}
