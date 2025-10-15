using System;
using System.Numerics;
using Nethereum.Util;

namespace Nethereum.Uniswap.V4
{
    public static class V4TickMath
    {
        public const int MIN_TICK = -887272;
        public const int MAX_TICK = 887272;
        public static readonly BigInteger MIN_SQRT_RATIO = BigInteger.Parse("4295128739");
        public static readonly BigInteger MAX_SQRT_RATIO = BigInteger.Parse("1461446703485210103287273052203988822378723970342");

        public static BigInteger GetSqrtRatioAtTick(int tick)
        {
            if (tick < MIN_TICK || tick > MAX_TICK)
                throw new ArgumentOutOfRangeException(nameof(tick), "Tick out of bounds");

            int absTick = tick < 0 ? -tick : tick;
            BigInteger ratio = (absTick & 0x1) != 0
                ? BigInteger.Parse("79232123823359799118286999567")
                : BigInteger.Parse("79228162514264337593543950336");

            if ((absTick & 0x2) != 0) ratio = (ratio * BigInteger.Parse("79236085330515764027303304731")) >> 96;
            if ((absTick & 0x4) != 0) ratio = (ratio * BigInteger.Parse("79244008939048815603706035061")) >> 96;
            if ((absTick & 0x8) != 0) ratio = (ratio * BigInteger.Parse("79259858533276714757314932305")) >> 96;
            if ((absTick & 0x10) != 0) ratio = (ratio * BigInteger.Parse("79291567232598584799939703904")) >> 96;
            if ((absTick & 0x20) != 0) ratio = (ratio * BigInteger.Parse("79355022692464371645785046466")) >> 96;
            if ((absTick & 0x40) != 0) ratio = (ratio * BigInteger.Parse("79482085999252804386437311141")) >> 96;
            if ((absTick & 0x80) != 0) ratio = (ratio * BigInteger.Parse("79736823300114093921829183326")) >> 96;
            if ((absTick & 0x100) != 0) ratio = (ratio * BigInteger.Parse("80248749790819932309965073892")) >> 96;
            if ((absTick & 0x200) != 0) ratio = (ratio * BigInteger.Parse("81282483887344747381513967011")) >> 96;
            if ((absTick & 0x400) != 0) ratio = (ratio * BigInteger.Parse("83390072131320151908154831281")) >> 96;
            if ((absTick & 0x800) != 0) ratio = (ratio * BigInteger.Parse("87770609709833776024991924138")) >> 96;
            if ((absTick & 0x1000) != 0) ratio = (ratio * BigInteger.Parse("97234110755111693312479820773")) >> 96;
            if ((absTick & 0x2000) != 0) ratio = (ratio * BigInteger.Parse("119332217159966728226237229890")) >> 96;
            if ((absTick & 0x4000) != 0) ratio = (ratio * BigInteger.Parse("179736315981702064433883588727")) >> 96;
            if ((absTick & 0x8000) != 0) ratio = (ratio * BigInteger.Parse("407748233172238350107850275304")) >> 96;
            if ((absTick & 0x10000) != 0) ratio = (ratio * BigInteger.Parse("2098478828474011932436660412517")) >> 96;
            if ((absTick & 0x20000) != 0) ratio = (ratio * BigInteger.Parse("55581415166113811149459800483533")) >> 96;
            if ((absTick & 0x40000) != 0) ratio = (ratio * BigInteger.Parse("38992368544603139932233054999993551")) >> 96;

            if (tick > 0) ratio = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935") / ratio;

            return (ratio >> 32) + (ratio % (BigInteger.One << 32) == 0 ? BigInteger.Zero : BigInteger.One);
        }

        public static int GetTickAtSqrtRatio(BigInteger sqrtPriceX96)
        {
            if (sqrtPriceX96 < MIN_SQRT_RATIO || sqrtPriceX96 >= MAX_SQRT_RATIO)
                throw new ArgumentOutOfRangeException(nameof(sqrtPriceX96), "SqrtPriceX96 out of bounds");

            BigInteger ratio = sqrtPriceX96 << 32;
            BigInteger r = ratio;
            BigInteger msb = 0;

            BigInteger f = (r > BigInteger.Parse("340282366920938463463374607431768211455")) ? BigInteger.One << 7 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("18446744073709551615")) ? BigInteger.One << 6 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("4294967295")) ? BigInteger.One << 5 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("65535")) ? BigInteger.One << 4 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("255")) ? BigInteger.One << 3 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("15")) ? BigInteger.One << 2 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("3")) ? BigInteger.One << 1 : BigInteger.Zero;
            msb = msb | f;
            r = r >> (int)f;

            f = (r > BigInteger.Parse("1")) ? BigInteger.One : BigInteger.Zero;
            msb = msb | f;

            if (msb >= 128) r = ratio >> (int)(msb - 127);
            else r = ratio << (int)(127 - msb);

            BigInteger log_2 = ((BigInteger)(msb - 128)) << 64;

            for (int i = 0; i < 14; i++)
            {
                r = (r * r) >> 127;
                BigInteger f_i = r >> 128;
                log_2 = log_2 | (f_i << (63 - i));
                r = r >> (int)f_i;
            }

            BigInteger log_sqrt10001 = log_2 * BigInteger.Parse("255738958999603826347141");
            int tickLow = (int)((log_sqrt10001 - BigInteger.Parse("3402992956809132418596140100660247210")) >> 128);
            int tickHi = (int)((log_sqrt10001 + BigInteger.Parse("291339464771989622907027621153398088495")) >> 128);

            return tickLow == tickHi ? tickLow : (GetSqrtRatioAtTick(tickHi) <= sqrtPriceX96 ? tickHi : tickLow);
        }

        public static decimal GetPriceAtTick(int tick)
        {
            var sqrtPriceX96 = GetSqrtRatioAtTick(tick);
            return V4PriceCalculator.CalculatePriceFromSqrtPriceX96(sqrtPriceX96);
        }

        public static decimal GetPriceAtTick(int tick, int decimals0, int decimals1)
        {
            var sqrtPriceX96 = GetSqrtRatioAtTick(tick);
            return V4PriceCalculator.CalculatePriceFromSqrtPriceX96(sqrtPriceX96, decimals0, decimals1);
        }

        public static int GetTickAtPrice(decimal price)
        {
            var sqrtPriceX96 = V4PriceCalculator.CalculateSqrtPriceX96FromPrice(price);
            return GetTickAtSqrtRatio(sqrtPriceX96);
        }

        public static int GetTickAtPrice(decimal price, int decimals0, int decimals1)
        {
            var sqrtPriceX96 = V4PriceCalculator.CalculateSqrtPriceX96FromPrice(price, decimals0, decimals1);
            return GetTickAtSqrtRatio(sqrtPriceX96);
        }

        public static int GetNearestUsableTick(int tick, int tickSpacing)
        {
            if (tickSpacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickSpacing), "Tick spacing must be positive");

            int rounded = (int)Math.Round((double)tick / tickSpacing) * tickSpacing;

            if (rounded < MIN_TICK) return MIN_TICK;
            if (rounded > MAX_TICK) return MAX_TICK;

            return rounded;
        }
    }
}
