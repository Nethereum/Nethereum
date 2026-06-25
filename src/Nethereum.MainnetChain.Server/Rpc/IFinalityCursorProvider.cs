using System.Numerics;

namespace Nethereum.MainnetChain.Server.Rpc
{
    /// <summary>
    /// Resolves the canonical block heights for the JSON-RPC <c>"finalized"</c> and
    /// <c>"safe"</c> labels per
    /// <see href="https://github.com/ethereum/execution-apis">execution-apis</see>. When a
    /// beacon light client is active, finalized derives from
    /// <c>LightClientState.FinalizedHeader.Execution.BlockNumber</c> and safe from
    /// <c>LightClientState.OptimisticHeader.Execution.BlockNumber</c>; without one the
    /// host degrades both labels to the latest committed block number.
    /// </summary>
    public interface IFinalityCursorProvider
    {
        BigInteger? GetFinalizedBlockNumber();
        BigInteger? GetSafeBlockNumber();
    }
}
