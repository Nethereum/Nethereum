using System.Numerics;

namespace Nethereum.CoreChain.Rpc
{
    public sealed class LatestOnlyFinalityCursorProvider : IFinalityCursorProvider
    {
        public BigInteger? GetFinalizedBlockNumber() => null;
        public BigInteger? GetSafeBlockNumber() => null;
    }
}
