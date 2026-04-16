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
        public byte[] StateRoot { get; set; }
        public byte[] TransactionsRoot { get; set; }
        public byte[] ReceiptsRoot { get; set; }
        public byte[] BlockHash { get; set; }
        public ExecutionStateService FinalExecutionState { get; set; }
        public InMemoryStateReader StateReader { get; set; }
    }
}
