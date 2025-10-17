namespace Nethereum.Uniswap.V4
{
    /// <summary>
    /// Constants and defaults for Uniswap V4 protocol
    /// </summary>
    public static class FeesAndTicksConstants
    {
        /// <summary>
        /// Common fee tiers used in Uniswap V4 pools.
        /// FeeCalculator values represent basis points (1/100th of a percent):
        /// - 100 = 0.01% (1 basis point)
        /// - 500 = 0.05% (5 basis points)
        /// - 3000 = 0.30% (30 basis points)
        /// - 10000 = 1.00% (100 basis points)
        /// </summary>
        public static readonly int[] CommonFeeTiers = new int[] { 100, 500, 3000, 10000 };

        /// <summary>
        /// Common tick spacings used in Uniswap V4 pools.
        /// Tick spacing determines the granularity of price ranges for liquidity positions.
        /// Typical values: 1, 10, 60, 200
        /// </summary>
        public static readonly int[] CommonTickSpacings = new int[] { 1, 10, 60, 200 };
    }
}
