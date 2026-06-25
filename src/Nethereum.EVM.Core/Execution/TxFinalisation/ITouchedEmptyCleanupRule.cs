using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// End-of-transaction strategy: should accounts marked
    /// <see cref="AccountExecutionState.IsTouched"/> and now empty be
    /// deleted from state? Per-fork registration on
    /// <see cref="HardforkConfig.TouchedEmptyCleanupRule"/>:
    /// - <see cref="NoOpTouchedEmptyCleanupRule"/> for Frontier through
    ///   Tangerine Whistle (pre-EIP-161).
    /// - <see cref="Eip161TouchedEmptyCleanupRule"/> for Spurious Dragon
    ///   onwards (EIP-161 STATE_CLEARING).
    /// </summary>
    public interface ITouchedEmptyCleanupRule
    {
        void Apply(ExecutionStateService executionState);
    }
}
