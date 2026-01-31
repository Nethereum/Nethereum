using System.Collections.Generic;
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
    }

    public class TransactionResult
    {
        public byte[] TxHash { get; set; }
        public bool Success { get; set; }
        public Receipt Receipt { get; set; }
        public string ErrorMessage { get; set; }
    }
}
