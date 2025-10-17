using System;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Pools
{
    public class PoolCacheEntry
    {
        public string PoolId { get; set; }
        public string Currency0 { get; set; }
        public string Currency1 { get; set; }
        public int Fee { get; set; }
        public int TickSpacing { get; set; }
        public string Hooks { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool Exists { get; set; }
    }
}





