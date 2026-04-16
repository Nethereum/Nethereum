using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM
{
    /// <summary>
    /// Computes a state root from post-execution account state.
    /// Pluggable: swap Patricia MPT for Verkle/binary Merkle without changing the EVM.
    /// </summary>
    public interface IStateRootCalculator
    {
        byte[] ComputeStateRoot(ExecutionStateService executionState);
    }
}
