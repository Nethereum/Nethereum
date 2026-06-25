using System.Numerics;

namespace Nethereum.MainnetChain.Server.Rpc
{
    public sealed class LatestOnlyFinalityCursorProvider : IFinalityCursorProvider
    {
        public BigInteger? GetFinalizedBlockNumber() => null;
        public BigInteger? GetSafeBlockNumber() => null;
    }
}
