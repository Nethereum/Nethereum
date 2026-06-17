using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;

namespace Nethereum.DevP2P.Sync.Strategies
{
    public class FullReExecutionStrategy : IExecutionStrategy
    {
        private readonly IBlockExecutor _blockExecutor;

        public FullReExecutionStrategy(IBlockExecutor blockExecutor)
        {
            _blockExecutor = blockExecutor;
        }

        public async Task<ExecutionStrategyResult> ExecuteAsync(LiveBlockData block, CancellationToken cancellationToken = default)
        {
            var result = await _blockExecutor.ProcessBlockAsync(
                block.Header,
                block.Transactions,
                uncles: new System.Collections.Generic.List<Nethereum.Model.BlockHeader>(),
                withdrawals: null,
                cancellationToken);
            return new ExecutionStrategyResult
            {
                Success = result.Exception == null && result.RootMatches,
                ComputedStateRoot = result.ComputedStateRoot,
                StateRootValidated = result.RootMatches,
                ErrorMessage = result.ErrorMessage
            };
        }
    }
}
