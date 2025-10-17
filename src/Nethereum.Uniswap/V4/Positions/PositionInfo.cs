using System.Numerics;

namespace Nethereum.Uniswap.V4.Positions
{
    public class PositionInfo
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
}





