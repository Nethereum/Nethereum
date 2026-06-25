using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// Pre-EIP-161: no touched-empty cleanup. Frontier through Tangerine
    /// Whistle keep all accounts in state regardless of touched/empty.
    /// </summary>
    public sealed class NoOpTouchedEmptyCleanupRule : ITouchedEmptyCleanupRule
    {
        public static readonly NoOpTouchedEmptyCleanupRule Instance = new NoOpTouchedEmptyCleanupRule();
        private NoOpTouchedEmptyCleanupRule() { }
        public void Apply(ExecutionStateService executionState) { }
    }
}
