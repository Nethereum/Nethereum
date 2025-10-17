using Nethereum.Uniswap.V4.Utils;
using System;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Positions
{
    public class LiquidityAmounts
    {
        public BigInteger Amount0 { get; set; }
        public BigInteger Amount1 { get; set; }
    }

    public class LiquidityCalculator
    {
        public static LiquidityCalculator Current { get; } = new LiquidityCalculator();
        private static readonly BigInteger Q96 = BigInteger.One << 96;

        public BigInteger GetLiquidityForAmount0(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount0)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            BigInteger intermediate = (sqrtRatioAX96 * sqrtRatioBX96) / Q96;
            return (amount0 * intermediate) / (sqrtRatioBX96 - sqrtRatioAX96);
        }

        public BigInteger GetLiquidityForAmount1(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount1)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            return (amount1 * Q96) / (sqrtRatioBX96 - sqrtRatioAX96);
        }

        public BigInteger GetLiquidityForAmounts(BigInteger sqrtRatioX96, BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount0, BigInteger amount1)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            if (sqrtRatioX96 <= sqrtRatioAX96)
            {
                return GetLiquidityForAmount0(sqrtRatioAX96, sqrtRatioBX96, amount0);
            }
            else if (sqrtRatioX96 < sqrtRatioBX96)
            {
                BigInteger liquidity0 = GetLiquidityForAmount0(sqrtRatioX96, sqrtRatioBX96, amount0);
                BigInteger liquidity1 = GetLiquidityForAmount1(sqrtRatioAX96, sqrtRatioX96, amount1);
                return liquidity0 < liquidity1 ? liquidity0 : liquidity1;
            }
            else
            {
                return GetLiquidityForAmount1(sqrtRatioAX96, sqrtRatioBX96, amount1);
            }
        }

        public BigInteger GetAmount0ForLiquidity(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger liquidity)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            return (liquidity * Q96 * (sqrtRatioBX96 - sqrtRatioAX96)) / sqrtRatioBX96 / sqrtRatioAX96;
        }

        public BigInteger GetAmount1ForLiquidity(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger liquidity)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            return (liquidity * (sqrtRatioBX96 - sqrtRatioAX96)) / Q96;
        }

        public LiquidityAmounts GetAmountsForLiquidity(BigInteger sqrtRatioX96, BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger liquidity)
        {
            if (sqrtRatioAX96 > sqrtRatioBX96)
            {
                var temp = sqrtRatioAX96;
                sqrtRatioAX96 = sqrtRatioBX96;
                sqrtRatioBX96 = temp;
            }

            BigInteger amount0 = 0;
            BigInteger amount1 = 0;

            if (sqrtRatioX96 <= sqrtRatioAX96)
            {
                amount0 = GetAmount0ForLiquidity(sqrtRatioAX96, sqrtRatioBX96, liquidity);
            }
            else if (sqrtRatioX96 < sqrtRatioBX96)
            {
                amount0 = GetAmount0ForLiquidity(sqrtRatioX96, sqrtRatioBX96, liquidity);
                amount1 = GetAmount1ForLiquidity(sqrtRatioAX96, sqrtRatioX96, liquidity);
            }
            else
            {
                amount1 = GetAmount1ForLiquidity(sqrtRatioAX96, sqrtRatioBX96, liquidity);
            }

            return new LiquidityAmounts
            {
                Amount0 = amount0,
                Amount1 = amount1
            };
        }

        public LiquidityAmounts GetAmountsForLiquidityByTicks(BigInteger sqrtRatioX96, int tickLower, int tickUpper, BigInteger liquidity)
        {
            var sqrtRatioAX96 = V4TickMath.Current.GetSqrtRatioAtTick(tickLower);
            var sqrtRatioBX96 = V4TickMath.Current.GetSqrtRatioAtTick(tickUpper);
            return GetAmountsForLiquidity(sqrtRatioX96, sqrtRatioAX96, sqrtRatioBX96, liquidity);
        }

        public BigInteger GetLiquidityForAmountsByTicks(BigInteger sqrtRatioX96, int tickLower, int tickUpper, BigInteger amount0, BigInteger amount1)
        {
            var sqrtRatioAX96 = V4TickMath.Current.GetSqrtRatioAtTick(tickLower);
            var sqrtRatioBX96 = V4TickMath.Current.GetSqrtRatioAtTick(tickUpper);
            return GetLiquidityForAmounts(sqrtRatioX96, sqrtRatioAX96, sqrtRatioBX96, amount0, amount1);
        }
    }
}





