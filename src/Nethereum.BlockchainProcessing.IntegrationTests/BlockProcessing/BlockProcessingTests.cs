using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{

    public class BlockProcessingTests : BlockProcessingTestBase
    {

        [Fact]
        public async Task Should_Crawl_Blocks_And_Invoke_Handling_Steps()
        {
            var blockToCrawl = new BigInteger(1);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: blockToCrawl, numberOfTransactions: 2, logsPerTransaction: 2);

            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(steps =>
            {
                steps.BlockStep.AddSynchronousProcessorHandler((block) => processedData.Blocks.Add(block));
                steps.TransactionStep.AddSynchronousProcessorHandler((tx) => processedData.Transactions.Add(tx));
                steps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedData.TransactionsWithReceipt.Add(tx));
                steps.ContractCreationStep.AddSynchronousProcessorHandler((c) => processedData.ContractCreations.Add(c));
                steps.FilterLogStep.AddSynchronousProcessorHandler((filterLog) => processedData.FilterLogs.Add(filterLog));
            });

            var cancellationTokenSource = new CancellationTokenSource();

            await blockProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            Assert.Single(processedData.Blocks);
            Assert.Equal(2, processedData.Transactions.Count);
            Assert.Equal(2, processedData.TransactionsWithReceipt.Count);
            Assert.Equal(4, processedData.FilterLogs.Count);
        }

        [Fact]
        public async Task Should_Not_Retrieve_Receipt_When_Tx_Does_Not_Meet_Handler_Criteria()
        {
            var blockToCrawl = new BigInteger(1);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: blockToCrawl, numberOfTransactions: 2, logsPerTransaction: 2);

            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(steps =>
            {
                steps.BlockStep.AddSynchronousProcessorHandler((block) => processedData.Blocks.Add(block));
                steps.TransactionStep.SetMatchCriteria((tx) => false);
                steps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedData.TransactionsWithReceipt.Add(tx));
            });

            var cancellationTokenSource = new CancellationTokenSource();

            await blockProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            Assert.Single(processedData.Blocks);
            Assert.Empty(processedData.Transactions);
            Assert.Empty(processedData.TransactionsWithReceipt);
            Assert.Equal(0, mockRpcResponses.ReceiptRequestCount);
        }

        [Fact]
        public async Task Should_Retrieve_Receipt_When_There_Is_No_Tx_Criteria()
        {
            var blockToCrawl = new BigInteger(1);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: blockToCrawl, numberOfTransactions: 2, logsPerTransaction: 2);

            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(steps =>
            {
                steps.BlockStep.AddSynchronousProcessorHandler((block) => processedData.Blocks.Add(block));
                steps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedData.TransactionsWithReceipt.Add(tx));
            });

            var cancellationTokenSource = new CancellationTokenSource();

            await blockProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            Assert.Single(processedData.Blocks);
            Assert.Equal(2, processedData.TransactionsWithReceipt.Count);
            Assert.Equal(2, mockRpcResponses.ReceiptRequestCount);
        }

        [Fact]
        public async Task When_Step_Criteria_Is_Set_Should_Invoke_Handler_If_Matched()
        {
            var blockToCrawl = new BigInteger(1);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: blockToCrawl, numberOfTransactions: 2, logsPerTransaction: 2);

            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(steps =>
            {
                steps.TransactionStep.SetMatchCriteria(tx => true);

                //create some criteria to prevent handling unwanted transactions
                //this rule will match the first transaction
                steps.TransactionReceiptStep.SetMatchCriteria(tx => tx.Transaction.TransactionIndex.Value == 0);
                steps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedData.TransactionsWithReceipt.Add(tx));

            });

            var cancellationTokenSource = new CancellationTokenSource();

            await blockProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            //we should have captured the first tx in the block
            Assert.Single(processedData.TransactionsWithReceipt);
        }

        [Fact]
        public async Task When_There_Is_Prior_Progress_Processing_Should_Pick_Up_From_Where_It_Left_Off()
        {
            //pretend we have already completed block 1
            var lastBlockProcessed = new BigInteger(1);
            var nextBlockExpected = lastBlockProcessed + 1;

            var progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: nextBlockExpected, numberOfTransactions: 2, logsPerTransaction: 2);

            var processedData = new ProcessedBlockchainData();

            var cancellationTokenSource = new CancellationTokenSource();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(progressRepository, steps =>
            {
                //capture a block and then cancel
                steps.BlockStep.AddSynchronousProcessorHandler((block) => {
                    processedData.Blocks.Add(block);
                    cancellationTokenSource.Cancel();
                });
            }
            );

            await blockProcessor.ExecuteAsync(cancellationTokenSource.Token);

            Assert.Single(processedData.Blocks); // one block processed
            Assert.Equal(nextBlockExpected, processedData.Blocks[0].Number.Value); // should have been the next block
            Assert.Equal(nextBlockExpected, await progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }

        [Fact]
        public async Task When_There_Is_Prior_Progress_A_Minimum_Starting_Block_Number_Will_Prevent_Processing_Earlier_Blocks()
        {
            var lastBlockProcessed = new BigInteger(5);
            var minBlock = new BigInteger(10);

            var progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            mockRpcResponses.AddToGetBlockNumberRequestQueue(100);
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: minBlock, numberOfTransactions: 2, logsPerTransaction: 2);

            var cancellationTokenSource = new CancellationTokenSource();
            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(progressRepository, steps =>
            {
                //capture a block and then cancel
                steps.BlockStep.AddSynchronousProcessorHandler((block) => {
                    processedData.Blocks.Add(block);
                    cancellationTokenSource.Cancel();
                });
            }
            );


            await blockProcessor.ExecuteAsync(cancellationTokenSource.Token, minBlock);

            Assert.Single(processedData.Blocks); // one block processed
            Assert.Equal(minBlock, processedData.Blocks[0].Number.Value); // should have been the next block
            Assert.Equal(minBlock, await progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }

        [Fact]
        public async Task Will_Wait_For_Block_Confirmations_Before_Processing()
        {
            var blockLastProcessed = new BigInteger(100);
            var nextBlock = blockLastProcessed + 1;
            const uint MIN_CONFIRMATIONS = 12;

            var progressRepository = new InMemoryBlockchainProgressRepository(blockLastProcessed);

            var mockRpcResponses = new BlockProcessingRpcMock(Web3Mock);
            //when first asked - pretend the current block is behind the required confirmations
            mockRpcResponses.AddToGetBlockNumberRequestQueue(blockLastProcessed + MIN_CONFIRMATIONS);
            //the next time return an incremented block which is under the confirmation limit
            mockRpcResponses.AddToGetBlockNumberRequestQueue(nextBlock + MIN_CONFIRMATIONS);

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: nextBlock, numberOfTransactions: 2, logsPerTransaction: 2);

            var cancellationTokenSource = new CancellationTokenSource();
            var processedData = new ProcessedBlockchainData();

            var blockProcessor = Web3.Processing.Blocks.CreateBlockProcessor(progressRepository, steps =>
            {
                //capture a block and then cancel
                steps.BlockStep.AddSynchronousProcessorHandler((block) => {
                    processedData.Blocks.Add(block);
                    cancellationTokenSource.Cancel();
                });
            }
            , MIN_CONFIRMATIONS);


            await blockProcessor.ExecuteAsync(cancellationTokenSource.Token);

            Assert.Single(processedData.Blocks); //should have processed a single block before cancellation
            Assert.Equal(2, mockRpcResponses.BlockNumberRequestCount); //should have asked for latest block twice
            Assert.Equal(nextBlock, processedData.Blocks[0].Number.Value); // should have handled the expected block
            Assert.Equal(nextBlock, await progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }
    }
}
