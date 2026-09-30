using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class BlockProductionResult
    {
        public BlockHeader Header { get; set; }
        public byte[] BlockHash { get; set; }
        public List<TransactionResult> TransactionResults { get; set; } = new();
        public int SuccessfulTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public byte[] WitnessBytes { get; set; }
        public byte[] PreStateRoot { get; set; }
        public IReadOnlyList<byte[]> ExecutionRequests { get; set; }
        public byte[]? BlockAccessListRlp { get; set; }
        public object? MessageBatchResult { get; set; }
        public List<ISignedTransaction> IncludedTransactions { get; set; } = new();
    }

    public class TransactionResult
    {
        public byte[] TxHash { get; set; }
        public string Sender { get; set; }
        public bool Success { get; set; }
        public bool IsRevert { get; set; }
        public bool IsOutOfGas { get; set; }
        public Receipt Receipt { get; set; }
        public List<Log> Logs { get; set; }
        public string ErrorMessage { get; set; }
        public BigInteger GasUsed { get; set; }
        public BigInteger GasRefund { get; set; }
        public byte[] ReturnData { get; set; }
    }
}
