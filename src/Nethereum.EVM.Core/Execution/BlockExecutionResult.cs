using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif

namespace Nethereum.EVM.Execution
{
    public class BlockExecutionResult
    {
        public List<TransactionExecutionResult> TxResults { get; set; }
        public List<Model.Receipt> Receipts { get; set; }
        public byte[] CombinedBloom { get; set; }
        public long CumulativeGasUsed { get; set; }
        public long HeaderGasUsed { get; set; }
        public long BlockExecutionGasUsed { get; set; }
        public long BlockStateGasUsed { get; set; }
        public byte[] StateRoot { get; set; }
        public byte[] TransactionsRoot { get; set; }
        public byte[] ReceiptsRoot { get; set; }
        public byte[] BlockHash { get; set; }

        public Model.BlockHeader ProducedHeader { get; set; }

        public System.Collections.Generic.List<Model.AccountChanges> BlockAccessList { get; set; }

        public bool BlockAccessListGasLimitExceeded { get; set; }

        public BlockAccessListStructureCheck DeclaredBlockAccessListCheck { get; set; }

        /// <summary>EIP-7928 §Engine API's "malformed" verdict, as opposed to its
        /// "doesn't match" one — see <see cref="BlockAccessListStructureRule"/>.</summary>
        public bool BlockAccessListMalformed => !DeclaredBlockAccessListCheck.IsWellFormed;

        public ExecutionStateService FinalExecutionState { get; set; }
        public InMemoryStateReader StateReader { get; set; }
    }
}
