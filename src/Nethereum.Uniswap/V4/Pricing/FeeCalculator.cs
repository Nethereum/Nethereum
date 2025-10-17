using System.Numerics;

namespace Nethereum.Uniswap.V4.Pricing
{
    public class UnclaimedFees
    {
        public BigInteger Fees0 { get; set; }
        public BigInteger Fees1 { get; set; }
    }

    public class FeeCalculator
    {
        public static FeeCalculator Current { get; } = new FeeCalculator();
        private static readonly BigInteger Q128 = BigInteger.One << 128;

        public UnclaimedFees CalculateUnclaimedFees(
            BigInteger liquidity,
            BigInteger feeGrowthInside0LastX128,
            BigInteger feeGrowthInside1LastX128,
            BigInteger feeGrowthInside0CurrentX128,
            BigInteger feeGrowthInside1CurrentX128)
        {
            var fees0 = CalculateFees(liquidity, feeGrowthInside0LastX128, feeGrowthInside0CurrentX128);
            var fees1 = CalculateFees(liquidity, feeGrowthInside1LastX128, feeGrowthInside1CurrentX128);

            return new UnclaimedFees
            {
                Fees0 = fees0,
                Fees1 = fees1
            };
        }

        private BigInteger CalculateFees(
            BigInteger liquidity,
            BigInteger feeGrowthLastX128,
            BigInteger feeGrowthCurrentX128)
        {
            if (liquidity == 0)
            {
                return 0;
            }

            BigInteger feeGrowthDelta;
            if (feeGrowthCurrentX128 >= feeGrowthLastX128)
            {
                feeGrowthDelta = feeGrowthCurrentX128 - feeGrowthLastX128;
            }
            else
            {
                feeGrowthDelta = (BigInteger.Pow(2, 256) - feeGrowthLastX128) + feeGrowthCurrentX128;
            }

            var fees = (liquidity * feeGrowthDelta) / Q128;
            return fees;
        }
    }
}


