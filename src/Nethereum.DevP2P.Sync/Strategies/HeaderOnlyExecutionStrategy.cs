using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;

namespace Nethereum.DevP2P.Sync.Strategies
{
    /// <summary>
    /// Skips re-execution. Suitable for indexer/light-follower targets that
    /// only need header chain + bodies/receipts but do not maintain state.
    /// </summary>
    public class HeaderOnlyExecutionStrategy : IExecutionStrategy
    {
        public Task<ExecutionStrategyResult> ExecuteAsync(LiveBlockData block, CancellationToken cancellationToken = default)
            => Task.FromResult(ExecutionStrategyResult.Skipped());
    }
}
