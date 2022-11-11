using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.UnitTests.BlockProcessing
{
    public class ProcessedBlockchainData
    {
        public List<BlockWithTransactions> Blocks { get; set; } = new List<BlockWithTransactions>();
        public List<TransactionVO> Transactions { get; set; } = new List<TransactionVO>();
        public List<TransactionReceiptVO> TransactionsWithReceipt { get; set; } = new List<TransactionReceiptVO>();
        public List<ContractCreationVO> ContractCreations { get; set; } = new List<ContractCreationVO>();
        public List<FilterLogVO> FilterLogs { get; set; } = new List<FilterLogVO>();
    }
}
