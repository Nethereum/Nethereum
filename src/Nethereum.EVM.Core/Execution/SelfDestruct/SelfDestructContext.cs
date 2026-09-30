using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.SelfDestruct
{
    public struct SelfDestructContext
    {
        public Program Program;
        public string ContractAddress;
        public string RecipientAddress;
        public EvmUInt256 ContractBalance;
        public AccountExecutionState ContractAccount;
        public ExecutionStateService ExecutionStateService;
    }
}
