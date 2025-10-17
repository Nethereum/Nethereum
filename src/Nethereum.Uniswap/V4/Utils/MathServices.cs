using Nethereum.Uniswap.V4.Positions;
using Nethereum.Uniswap.V4.Pricing;

namespace Nethereum.Uniswap.V4.Utils
{
    /// <summary>
    /// Lightweight container exposing math helpers for Uniswap V4.
    /// </summary>
    public class MathServices
    {
        public V4TickMath Tick => V4TickMath.Current;
    }
}
