using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM
{
    public interface IStateRootCalculator
    {
        byte[] ComputeStateRoot(ExecutionStateService executionState);
    }
}
