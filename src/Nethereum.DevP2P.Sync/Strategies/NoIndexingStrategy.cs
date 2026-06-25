using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;

namespace Nethereum.DevP2P.Sync.Strategies
{
    public class NoIndexingStrategy : IIndexingStrategy
    {
        public Task IndexAsync(LiveBlockData block, ExecutionStrategyResult execution, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
