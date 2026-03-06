using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public interface IBlockReExecutor
    {
        Task<BlockReExecutionResult> ReExecuteBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            CancellationToken cancellationToken = default);
    }

    public class BlockReExecutionResult
    {
        public bool Success { get; set; }
        public byte[]? ComputedStateRoot { get; set; }
        public byte[]? ExpectedStateRoot { get; set; }
        public bool StateRootMatches { get; set; }
        public string? ErrorMessage { get; set; }
        public int TransactionsExecuted { get; set; }
        public List<TransactionReExecutionResult> TransactionResults { get; set; } = new();
    }

    public class TransactionReExecutionResult
    {
        public byte[]? TransactionHash { get; set; }
        public bool Success { get; set; }
        public long GasUsed { get; set; }
        public string? Error { get; set; }
    }
}
