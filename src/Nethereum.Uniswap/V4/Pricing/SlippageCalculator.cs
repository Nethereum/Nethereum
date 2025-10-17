using Nethereum.Util;
using System;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Pricing
{
    public class SlippageResult
    {
        public BigInteger OriginalAmount { get; set; }
        public BigInteger AmountWithSlippage { get; set; }
        public BigDecimal SlippageTolerancePercentage { get; set; }
        public BigInteger SlippageAmount { get; set; }
    }

    public class SlippageValidationResult
    {
        public bool IsValid { get; set; }
        public BigDecimal ActualSlippagePercentage { get; set; }
        public BigDecimal TolerancePercentage { get; set; }
        public string Message { get; set; }
    }

    public class MinimumLiquidityAmounts
    {
        public BigInteger MinAmount0 { get; set; }
        public BigInteger MinAmount1 { get; set; }
    }

    public  class SlippageCalculator
    {
        public static readonly SlippageCalculator Current = new SlippageCalculator();
        private  readonly BigDecimal Zero = new BigDecimal(BigInteger.Zero, 0);
        private  readonly BigDecimal One = new BigDecimal(BigInteger.One, 0);
        private  readonly BigDecimal Hundred = new BigDecimal(new BigInteger(100), 0);

        public  SlippageResult CalculateMinimumAmountOut(BigInteger amountOut, BigDecimal slippageTolerancePercentage)
        {
            ValidatePositiveAmount(amountOut, nameof(amountOut));
            ValidateTolerance(slippageTolerancePercentage);

            var factor = One - (slippageTolerancePercentage / Hundred);
            var minimumAmount = (BigInteger)(new BigDecimal(amountOut, 0) * factor);
            if (minimumAmount < BigInteger.Zero)
            {
                minimumAmount = BigInteger.Zero;
            }

            var slippageAmount = amountOut - minimumAmount;

            return new SlippageResult
            {
                OriginalAmount = amountOut,
                AmountWithSlippage = minimumAmount,
                SlippageTolerancePercentage = slippageTolerancePercentage,
                SlippageAmount = slippageAmount
            };
        }

        public  SlippageResult CalculateMaximumAmountIn(BigInteger amountIn, BigDecimal slippageTolerancePercentage)
        {
            ValidatePositiveAmount(amountIn, nameof(amountIn));
            ValidateTolerance(slippageTolerancePercentage);

            var factor = One + (slippageTolerancePercentage / Hundred);
            var maximumAmount = CeilingToBigInteger(new BigDecimal(amountIn, 0) * factor);
            if (maximumAmount < BigInteger.Zero)
            {
                maximumAmount = BigInteger.Zero;
            }

            var slippageAmount = maximumAmount - amountIn;

            return new SlippageResult
            {
                OriginalAmount = amountIn,
                AmountWithSlippage = maximumAmount,
                SlippageTolerancePercentage = slippageTolerancePercentage,
                SlippageAmount = slippageAmount
            };
        }

        public  BigDecimal CalculateSlippagePercentage(BigInteger expectedAmount, BigInteger actualAmount)
        {
            if (expectedAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedAmount), "Expected amount must be positive");
            }

            if (actualAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actualAmount), "Actual amount cannot be negative");
            }

            var difference = BigInteger.Abs(expectedAmount - actualAmount);
            if (difference.IsZero)
            {
                return Zero;
            }

            var ratio = new BigDecimal(difference, 0) / new BigDecimal(expectedAmount, 0);
            return ratio * Hundred;
        }

        public  SlippageValidationResult ValidateSlippage(
            BigInteger expectedAmount,
            BigInteger actualAmount,
            BigDecimal slippageTolerancePercentage,
            bool isAmountOut = true)
        {
            if (expectedAmount <= 0)
            {
                return new SlippageValidationResult
                {
                    IsValid = false,
                    ActualSlippagePercentage = Zero,
                    TolerancePercentage = slippageTolerancePercentage,
                    Message = "Expected amount must be positive"
                };
            }

            ValidateTolerance(slippageTolerancePercentage);

            var actualSlippage = CalculateSlippagePercentage(expectedAmount, actualAmount);
            bool isValid;
            string message;

            if (isAmountOut)
            {
                isValid = actualAmount >= expectedAmount || actualSlippage <= slippageTolerancePercentage;
                message = isValid
                    ? $"Slippage {actualSlippage}% is within tolerance {slippageTolerancePercentage}%"
                    : $"Slippage {actualSlippage}% exceeds tolerance {slippageTolerancePercentage}%";
            }
            else
            {
                isValid = actualAmount <= expectedAmount || actualSlippage <= slippageTolerancePercentage;
                message = isValid
                    ? $"Slippage {actualSlippage}% is within tolerance {slippageTolerancePercentage}%"
                    : $"Slippage {actualSlippage}% exceeds tolerance {slippageTolerancePercentage}%";
            }

            return new SlippageValidationResult
            {
                IsValid = isValid,
                ActualSlippagePercentage = actualSlippage,
                TolerancePercentage = slippageTolerancePercentage,
                Message = message
            };
        }

        public  MinimumLiquidityAmounts CalculateMinimumLiquidityAmounts(
            BigInteger amount0,
            BigInteger amount1,
            BigDecimal slippageTolerancePercentage)
        {
            var result0 = CalculateMinimumAmountOut(amount0, slippageTolerancePercentage);
            var result1 = CalculateMinimumAmountOut(amount1, slippageTolerancePercentage);

            return new MinimumLiquidityAmounts { MinAmount0 = result0.AmountWithSlippage, MinAmount1 = result1.AmountWithSlippage };
        }

        public  BigInteger ApplySlippageTolerance(BigInteger amount, BigDecimal slippageTolerancePercentage, bool isMinimum)
        {
            return isMinimum
                ? CalculateMinimumAmountOut(amount, slippageTolerancePercentage).AmountWithSlippage
                : CalculateMaximumAmountIn(amount, slippageTolerancePercentage).AmountWithSlippage;
        }

        private  void ValidatePositiveAmount(BigInteger amount, string parameterName)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Amount must be positive");
            }
        }

        private  void ValidateTolerance(BigDecimal tolerance)
        {
            if (tolerance.CompareTo(Zero) < 0 || tolerance.CompareTo(Hundred) > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Slippage tolerance must be between 0 and 100");
            }
        }
        private  BigInteger CeilingToBigInteger(BigDecimal value)
        {
            if (value.CompareTo(Zero) >= 0)
            {
                var floor = value.FloorToBigInteger();
                var floorDecimal = new BigDecimal(floor, 0);
                return value.CompareTo(floorDecimal) > 0 ? floor + BigInteger.One : floor;
            }

            var negated = -value;
            var floorNegated = negated.FloorToBigInteger();
            return -floorNegated;
        }
    }
}

