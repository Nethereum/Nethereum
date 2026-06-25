using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Strategy for what data a follower extracts and forwards per block.
    /// Implementations:
    /// - NoIndexingStrategy: no-op (pure sync follower)
    /// - FilteredLogIndexingStrategy: extract logs matching address/topic filters
    /// - FullBlockIndexingStrategy: forward header+body+receipts to downstream sink
    /// </summary>
    public interface IIndexingStrategy
    {
        Task IndexAsync(LiveBlockData block, ExecutionStrategyResult execution, CancellationToken cancellationToken = default);
    }
}
