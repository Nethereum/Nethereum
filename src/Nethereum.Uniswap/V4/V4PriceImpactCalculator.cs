using Nethereum.Util;
using System;
using System.Numerics;

namespace Nethereum.Uniswap.V4
{
    public enum PriceImpactLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class PriceImpactResult
    {
        public decimal PriceImpactPercentage { get; set; }
        public PriceImpactLevel ImpactLevel { get; set; }
        public string Message { get; set; }
        public bool ShouldWarn { get; set; }
        public decimal PriceBefore { get; set; }
        public decimal PriceAfter { get; set; }
    }

    public class V4SwapQuoteWithImpact
    {
        public BigInteger AmountIn { get; set; }
        public BigInteger AmountOut { get; set; }
        public BigInteger MinimumAmountOut { get; set; }
        public BigInteger MaximumAmountIn { get; set; }
        public decimal PriceImpactPercentage { get; set; }
        public PriceImpactLevel ImpactLevel { get; set; }
        public BigDecimal SlippageTolerancePercentage { get; set; }
        public decimal PriceBefore { get; set; }
        public decimal PriceAfter { get; set; }
        public BigInteger GasEstimate { get; set; }
        public bool IsExcessiveImpact { get; set; }
        public string Warning { get; set; }
    }

    public static class V4PriceImpactCalculator
    {
        private const decimal LOW_IMPACT_THRESHOLD = 1.0m;
        private const decimal MEDIUM_IMPACT_THRESHOLD = 3.0m;
        private const decimal HIGH_IMPACT_THRESHOLD = 5.0m;

        public static PriceImpactResult CalculatePriceImpact(decimal priceBefore, decimal priceAfter)
        {
            if (priceBefore <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priceBefore), "Price before must be positive");
            }

            if (priceAfter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priceAfter), "Price after cannot be negative");
            }

            var priceChange = Math.Abs(priceAfter - priceBefore);
            var impactPercentage = (priceChange / priceBefore) * 100m;

            var level = GetPriceImpactLevel(impactPercentage);
            var shouldWarn = level >= PriceImpactLevel.High;

            return new PriceImpactResult
            {
                PriceImpactPercentage = impactPercentage,
                ImpactLevel = level,
                ShouldWarn = shouldWarn,
                Message = GetImpactMessage(impactPercentage, level),
                PriceBefore = priceBefore,
                PriceAfter = priceAfter
            };
        }

        public static PriceImpactResult CalculatePriceImpactFromReserves(
            BigInteger amountIn,
            BigInteger reserveIn,
            BigInteger reserveOut)
        {
            if (reserveIn <= 0 || reserveOut <= 0)
            {
                throw new ArgumentException("Reserves must be positive");
            }

            if (amountIn <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountIn), "Amount in must be positive");
            }

            var priceBefore = (decimal)reserveOut / (decimal)reserveIn;
            var newReserveIn = reserveIn + amountIn;
            var amountOut = (reserveOut * amountIn) / newReserveIn;
            var newReserveOut = reserveOut - amountOut;

            var priceAfter = newReserveOut > 0 ? (decimal)newReserveOut / (decimal)newReserveIn : 0;

            return CalculatePriceImpact(priceBefore, priceAfter);
        }

        public static PriceImpactLevel GetPriceImpactLevel(decimal impactPercentage)
        {
            if (impactPercentage < LOW_IMPACT_THRESHOLD)
            {
                return PriceImpactLevel.Low;
            }
            else if (impactPercentage < MEDIUM_IMPACT_THRESHOLD)
            {
                return PriceImpactLevel.Medium;
            }
            else if (impactPercentage < HIGH_IMPACT_THRESHOLD)
            {
                return PriceImpactLevel.High;
            }
            else
            {
                return PriceImpactLevel.Critical;
            }
        }

        public static bool IsExcessivePriceImpact(decimal impactPercentage, decimal thresholdPercentage = HIGH_IMPACT_THRESHOLD)
        {
            return impactPercentage >= thresholdPercentage;
        }

        public static V4SwapQuoteWithImpact CreateQuoteWithImpact(
            BigInteger amountIn,
            BigInteger amountOut,
            BigDecimal slippageTolerancePercentage,
            decimal priceBefore,
            decimal priceAfter,
            BigInteger gasEstimate = default)
        {
            var minimumAmountOut = V4SlippageCalculator.CalculateMinimumAmountOut(amountOut, slippageTolerancePercentage);
            var maximumAmountIn = V4SlippageCalculator.CalculateMaximumAmountIn(amountIn, slippageTolerancePercentage);
            var priceImpact = CalculatePriceImpact(priceBefore, priceAfter);

            return new V4SwapQuoteWithImpact
            {
                AmountIn = amountIn,
                AmountOut = amountOut,
                MinimumAmountOut = minimumAmountOut.AmountWithSlippage,
                MaximumAmountIn = maximumAmountIn.AmountWithSlippage,
                PriceImpactPercentage = priceImpact.PriceImpactPercentage,
                ImpactLevel = priceImpact.ImpactLevel,
                SlippageTolerancePercentage = slippageTolerancePercentage,
                PriceBefore = priceBefore,
                PriceAfter = priceAfter,
                GasEstimate = gasEstimate,
                IsExcessiveImpact = priceImpact.ShouldWarn,
                Warning = priceImpact.ShouldWarn ? priceImpact.Message : null
            };
        }

        private static string GetImpactMessage(decimal impactPercentage, PriceImpactLevel level)
        {
            switch (level)
            {
                case PriceImpactLevel.Low:
                    return $"Price impact is low ({impactPercentage:F2}%). Trade is safe to execute.";
                case PriceImpactLevel.Medium:
                    return $"Price impact is moderate ({impactPercentage:F2}%). Consider the trade size.";
                case PriceImpactLevel.High:
                    return $"Price impact is high ({impactPercentage:F2}%). You may get a significantly worse price. Consider reducing trade size.";
                case PriceImpactLevel.Critical:
                    return $"Price impact is critical ({impactPercentage:F2}%). This trade will significantly move the market price. Strongly consider reducing trade size or splitting the trade.";
                default:
                    return $"Price impact: {impactPercentage:F2}%";
            }
        }

        public static decimal CalculateEffectivePrice(BigInteger amountIn, BigInteger amountOut, int decimalsIn, int decimalsOut)
        {
            if (amountOut == 0)
            {
                return 0;
            }

            var amountInDecimal = (decimal)amountIn / (decimal)Math.Pow(10, decimalsIn);
            var amountOutDecimal = (decimal)amountOut / (decimal)Math.Pow(10, decimalsOut);

            return amountInDecimal / amountOutDecimal;
        }

        public static decimal CalculatePriceImpactFromEffectivePrices(decimal spotPrice, decimal effectivePrice)
        {
            if (spotPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spotPrice), "Spot price must be positive");
            }

            return Math.Abs((effectivePrice - spotPrice) / spotPrice) * 100m;
        }
    }
}
