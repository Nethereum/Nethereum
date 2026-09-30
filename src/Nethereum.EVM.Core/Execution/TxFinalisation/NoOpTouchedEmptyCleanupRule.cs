using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    public sealed class NoOpTouchedEmptyCleanupRule : ITouchedEmptyCleanupRule
    {
        public static readonly NoOpTouchedEmptyCleanupRule Instance = new NoOpTouchedEmptyCleanupRule();
        private NoOpTouchedEmptyCleanupRule() { }
        public void Apply(ExecutionStateService executionState) { }
    }
}
