using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    public interface ITouchedEmptyCleanupRule
    {
        void Apply(ExecutionStateService executionState);
    }
}
