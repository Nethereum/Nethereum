using System.Numerics;

namespace Nethereum.Uniswap.V4.Positions
{
    public class PositionValueResult
    {
        public BigInteger Amount0 { get; set; }
        public BigInteger Amount1 { get; set; }
        public BigInteger UnclaimedFees0 { get; set; }
        public BigInteger UnclaimedFees1 { get; set; }
        public BigInteger TotalAmount0 { get; set; }
        public BigInteger TotalAmount1 { get; set; }
        public decimal ValueInToken0 { get; set; }
        public decimal ValueInToken1 { get; set; }
    }
}



