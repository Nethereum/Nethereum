using System;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Pools
{
    public class PoolInfo
    {
        public byte[] PoolId { get; set; }
        public string Currency0 { get; set; }
        public string Currency1 { get; set; }
        public uint Fee { get; set; }
        public int TickSpacing { get; set; }
        public string Hooks { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
        public ulong BlockNumber { get; set; }

        public decimal Price
        {
            get
            {
                if (SqrtPriceX96 == 0) return 0;
                var sqrtPrice = (decimal)SqrtPriceX96 / (decimal)BigInteger.Pow(2, 96);
                return sqrtPrice * sqrtPrice;
            }
        }

        public string GetPoolDescription()
        {
            return $"{Currency0}/{Currency1} Fee:{Fee} TickSpacing:{TickSpacing} Price:{Price:F6} Tick:{Tick}";
        }
    }
}
