using System.Numerics;

namespace Nethereum.CoreChain.Rpc
{
    public interface IFinalityCursorProvider
    {
        BigInteger? GetFinalizedBlockNumber();
        BigInteger? GetSafeBlockNumber();
    }
}
