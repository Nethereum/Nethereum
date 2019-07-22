using Moq;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{
    public class BlockingProcessingRpcMock
    {
        public BlockingProcessingRpcMock(Web3Mock web3Mock)
        {

            web3Mock.GetBlockWithTransactionsByNumberMock.Setup(s => s.SendRequestAsync(It.IsAny<HexBigInteger>(), null))
                .Returns<HexBigInteger, object>((n, id) =>
                {
                    return Task.FromResult(Blocks.FirstOrDefault(b => b.Number.Value == n.Value));
                });

            web3Mock.GetTransactionReceiptMock.Setup(s => s.SendRequestAsync(It.IsAny<string>(), null))
                .Returns<string, object>((hash, id) =>
                {
                    return Task.FromResult(Receipts.FirstOrDefault(b => b.TransactionHash == hash));
                });
        }

        public List<BlockWithTransactions> Blocks { get; set; } = new List<BlockWithTransactions>();

        public List<TransactionReceipt> Receipts { get; set; } = new List<TransactionReceipt>();

        public void AddTransactionsWithReceipts(int blockNumber, int numberOfTransactions, int logsPerTransaction)
        {
            var transactions = new Transaction[numberOfTransactions];

            for (var i = 0; i < numberOfTransactions; i++)
            {
                transactions[i] = new Transaction
                {
                    TransactionHash = $"0x{blockNumber}{i}ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd",
                    TransactionIndex = new HexBigInteger(i)
                };
            }

            Blocks.Add(new BlockWithTransactions
            {
                Number = new HexBigInteger(blockNumber),
                Transactions = transactions
            });

            foreach (var tx in transactions)
            {
                var logs = new FilterLog[logsPerTransaction];

                for (var l = 0; l < logsPerTransaction; l++)
                {
                    logs[l] = new FilterLog();
                }

                Receipts.AddRange(new[] {new TransactionReceipt {
                        TransactionHash = tx.TransactionHash,
                        Logs = logs.ConvertToJArray() },
                    });
            }
        }

    }
}
