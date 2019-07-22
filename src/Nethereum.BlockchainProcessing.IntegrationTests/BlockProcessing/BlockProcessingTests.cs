using Moq;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{
    public class BlockingProcessingRpcMock
    {
        public BlockingProcessingRpcMock(Web3Mock web3Mock)
        {

            web3Mock.GetBlockWithTransactionsByNumberMock.Setup(s => s.SendRequestAsync(It.IsAny<HexBigInteger>(), null))
                .Returns<HexBigInteger, object>((n, id) =>
                {
                    return Task.FromResult(Blocks.FirstOrDefault(b => b.Number == n));
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
                transactions[0] = new Transaction
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

    public class ProcessedBlockchainData
    {
        public List<BlockWithTransactions> Blocks { get; set; } = new List<BlockWithTransactions>();
        public List<TransactionVO> Transactions { get; set; } = new List<TransactionVO>();
        public List<TransactionReceiptVO> TransactionsWithReceipt { get; set; } = new List<TransactionReceiptVO>();
        public List<ContractCreationVO> ContractCreations { get; set; } = new List<ContractCreationVO>();
        public List<FilterLogVO> FilterLogs { get; set; } = new List<FilterLogVO>();
    }

    public class BlockProcessingTestBase
    {
        protected Web3Mock Web3Mock;
        protected BlockProcessingSteps processingSteps;
        protected BlockCrawlOrchestrator orchestrator;
        protected InMemoryBlockchainProgressRepository progressRepository;
        protected LastConfirmedBlockNumberService lastConfirmedBlockService;
        protected BlockchainProcessor blockchainProcessor;
        protected Queue<HexBigInteger> blockNumberQueue;
        protected BlockingProcessingRpcMock mockRpcResponses;
        protected ProcessedBlockchainData processedBlockchainData;

        public BlockProcessingTestBase()
        {
            Web3Mock = new Web3Mock();
            processedBlockchainData = new ProcessedBlockchainData();
        }

        protected virtual void Initialise()
        {
            processingSteps = new BlockProcessingSteps();
            orchestrator = new BlockCrawlOrchestrator(Web3Mock.EthApiContractServiceMock.Object, new[] { processingSteps });
            progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed: null);
            lastConfirmedBlockService = new LastConfirmedBlockNumberService(Web3Mock.BlockNumberMock.Object);
            blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);
            blockNumberQueue = new Queue<HexBigInteger>();

            Web3Mock
                .BlockNumberMock
                .Setup(m => m.SendRequestAsync(null))
                .Returns(() => Task.FromResult(blockNumberQueue.Dequeue()));

            mockRpcResponses = new BlockingProcessingRpcMock(Web3Mock);
        }

        protected virtual void MockGetBlockNumber(BigInteger blockNumberToReturn)
        {
            blockNumberQueue.Enqueue(new HexBigInteger(blockNumberToReturn));
        }
    }


    public class BlockProcessingTests : BlockProcessingTestBase
    {

        [Fact]
        public async Task ShouldCrawlBlocksAndInvokeHandlingSteps()
        {
            Initialise();
            MockGetBlockNumber(100);

            mockRpcResponses.AddTransactionsWithReceipts(blockNumber: 1, numberOfTransactions: 2, logsPerTransaction: 2);

            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => processedBlockchainData.Blocks.Add(block));
            processingSteps.TransactionStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.Transactions.Add(tx));
            processingSteps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.TransactionsWithReceipt.Add(tx));
            processingSteps.ContractCreationStep.AddSynchronousProcessorHandler((c) => processedBlockchainData.ContractCreations.Add(c));
            processingSteps.FilterLogStep.AddSynchronousProcessorHandler((filterLog) => processedBlockchainData.FilterLogs.Add(filterLog));

            var blockToCrawl = new BigInteger(1);

            var cancellationTokenSource = new CancellationTokenSource();

            await blockchainProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);


        }
    }
}
