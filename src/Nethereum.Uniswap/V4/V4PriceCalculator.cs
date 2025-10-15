using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Uniswap.V4.Contracts.PoolManager;
using Nethereum.Util;

namespace Nethereum.Uniswap.V4
{
    public class V4PoolPrice
    {
        public byte[] PoolId { get; set; }
        public string Currency0 { get; set; }
        public string Currency1 { get; set; }
        public decimal PriceCurrency0InCurrency1 { get; set; }
        public decimal PriceCurrency1InCurrency0 { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
    }

    public class V4PriceCalculator
    {
        public static decimal CalculatePriceFromSqrtPriceX96(BigInteger sqrtPriceX96)
        {
            if (sqrtPriceX96 == 0) return 0;
            var sqrtPrice = sqrtPriceX96 / new BigDecimal(BigInteger.Pow(2, 96));
            return (decimal)(sqrtPrice * sqrtPrice);
        }

        public static decimal CalculatePriceFromSqrtPriceX96(BigInteger sqrtPriceX96, int decimals0, int decimals1)
        {
            if (sqrtPriceX96 == 0) return 0;
            var sqrtRatio = sqrtPriceX96 / new BigDecimal(BigInteger.Pow(2, 96));
            var decimalFactor = BigInteger.Pow(10, decimals1) / new BigDecimal(BigInteger.Pow(10, decimals0));
            return (decimal)((sqrtRatio * sqrtRatio) / decimalFactor);
        }

        public static BigInteger CalculateSqrtPriceX96FromPrice(decimal price)
        {
            var sqrtPrice = new BigDecimal((double)System.Math.Sqrt((double)price));
            return (BigInteger)(sqrtPrice * new BigDecimal(BigInteger.Pow(2, 96)));
        }

        public static BigInteger CalculateSqrtPriceX96FromPrice(decimal price, int decimals0, int decimals1)
        {
            var decimalFactor = BigInteger.Pow(10, decimals1) / new BigDecimal(BigInteger.Pow(10, decimals0));
            var adjustedPrice = new BigDecimal(price) * decimalFactor;
            var sqrtPrice = new BigDecimal((double)System.Math.Sqrt((double)adjustedPrice));
            return (BigInteger)(sqrtPrice * new BigDecimal(BigInteger.Pow(2, 96)));
        }

        public static V4PoolPrice CreatePoolPrice(byte[] poolId, string currency0, string currency1, BigInteger sqrtPriceX96, int tick)
        {
            var price = CalculatePriceFromSqrtPriceX96(sqrtPriceX96);
            return new V4PoolPrice
            {
                PoolId = poolId,
                Currency0 = currency0,
                Currency1 = currency1,
                SqrtPriceX96 = sqrtPriceX96,
                Tick = tick,
                PriceCurrency0InCurrency1 = price,
                PriceCurrency1InCurrency0 = price == 0 ? 0 : 1 / price
            };
        }

        public static V4PoolPrice CreatePoolPrice(byte[] poolId, string currency0, string currency1, BigInteger sqrtPriceX96, int tick, int decimals0, int decimals1)
        {
            var price = CalculatePriceFromSqrtPriceX96(sqrtPriceX96, decimals0, decimals1);
            return new V4PoolPrice
            {
                PoolId = poolId,
                Currency0 = currency0,
                Currency1 = currency1,
                SqrtPriceX96 = sqrtPriceX96,
                Tick = tick,
                PriceCurrency0InCurrency1 = price,
                PriceCurrency1InCurrency0 = price == 0 ? 0 : 1 / price
            };
        }
    }
}
