using Nethereum.BlockchainProcessing.Processor;
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
            Initialise(lastBlockProcessed: null);
            mockRpcResponses.MockGetBlockNumber(100);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: 1, numberOfTransactions: 2, logsPerTransaction: 2);

            //note: there is no criteria set - so we handle everything
            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => processedBlockchainData.Blocks.Add(block));
            processingSteps.TransactionStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.Transactions.Add(tx));
            processingSteps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.TransactionsWithReceipt.Add(tx));
            processingSteps.ContractCreationStep.AddSynchronousProcessorHandler((c) => processedBlockchainData.ContractCreations.Add(c));
            processingSteps.FilterLogStep.AddSynchronousProcessorHandler((filterLog) => processedBlockchainData.FilterLogs.Add(filterLog));

            var blockToCrawl = new BigInteger(1);

            var cancellationTokenSource = new CancellationTokenSource();

            await blockchainProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            Assert.Single(processedBlockchainData.Blocks);
            Assert.Equal(2, processedBlockchainData.Transactions.Count);
            Assert.Equal(2, processedBlockchainData.TransactionsWithReceipt.Count);
            Assert.Equal(4, processedBlockchainData.FilterLogs.Count);

            Assert.Equal(blockToCrawl, await base.progressRepository.GetLastBlockNumberProcessedAsync());
        }

        [Fact]
        public async Task Should_Not_Retrieve_Receipt_When_Tx_Does_Not_Meet_Handler_Criteria()
        {
            Initialise(lastBlockProcessed: null);
            mockRpcResponses.MockGetBlockNumber(100);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: 1, numberOfTransactions: 2, logsPerTransaction: 2);

            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => processedBlockchainData.Blocks.Add(block));
            processingSteps.TransactionStep.SetMatchCriteria((tx) => false);
            processingSteps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.TransactionsWithReceipt.Add(tx));

            var blockToCrawl = new BigInteger(1);

            var cancellationTokenSource = new CancellationTokenSource();

            await blockchainProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            Assert.Single(processedBlockchainData.Blocks);
            Assert.Empty(processedBlockchainData.Transactions);
            Assert.Empty(processedBlockchainData.TransactionsWithReceipt);
            Assert.Equal(0, mockRpcResponses.ReceiptRequestCount);
        }

        [Fact]
        public async Task When_Step_Criteria_Is_Set_Should_Invoke_Handler_If_Matched()
        {
            Initialise(lastBlockProcessed: null);
            mockRpcResponses.MockGetBlockNumber(100);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            //we-re going to be given two transactions per block
            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: 1, numberOfTransactions: 2, logsPerTransaction: 2);

            processingSteps.TransactionStep.SetMatchCriteria(tx => true);

            //create some criteria to prevent handling unwanted transactions
            //this rule will match the first transaction
            processingSteps.TransactionReceiptStep.SetMatchCriteria(tx => tx.Transaction.TransactionIndex.Value == 0);
            processingSteps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => processedBlockchainData.TransactionsWithReceipt.Add(tx));

            var blockToCrawl = new BigInteger(1);

            var cancellationTokenSource = new CancellationTokenSource();

            await blockchainProcessor.ExecuteAsync(blockToCrawl, cancellationTokenSource.Token, blockToCrawl);

            //we should have captured the first tx in the block
            Assert.Single(processedBlockchainData.TransactionsWithReceipt);
        }

        [Fact]
        public async Task When_There_Is_Prior_Progress_Processing_Should_Pick_Up_From_Where_It_Left_Off()
        {
            //pretend we have already completed block 1
            var lastBlockProcessed = new BigInteger(1);
            Initialise(lastBlockProcessed: lastBlockProcessed);
            mockRpcResponses.MockGetBlockNumber(100);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            var nextBlockExpected = lastBlockProcessed + 1;

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: nextBlockExpected, numberOfTransactions: 2, logsPerTransaction: 2);

            var cancellationTokenSource = new CancellationTokenSource();

            //capture a block and then cancel
            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => {
                processedBlockchainData.Blocks.Add(block);
                cancellationTokenSource.Cancel();
            });

            await blockchainProcessor.ExecuteAsync(cancellationTokenSource.Token);

            Assert.Single(processedBlockchainData.Blocks); // one block processed
            Assert.Equal(nextBlockExpected, processedBlockchainData.Blocks[0].Number.Value); // should have been the next block
            Assert.Equal(nextBlockExpected, await base.progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }

        [Fact]
        public async Task When_There_Is_Prior_Progress_A_Minimum_Starting_Block_Number_Can_Prevent_Processing_Earlier_Blocks()
        {
            Initialise(lastBlockProcessed: null); //no prior progress
            mockRpcResponses.MockGetBlockNumber(100);

            var minBlock = new BigInteger(2);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: minBlock, numberOfTransactions: 2, logsPerTransaction: 2);

            var cancellationTokenSource = new CancellationTokenSource();

            //capture a block and then cancel
            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => {
                processedBlockchainData.Blocks.Add(block);
                cancellationTokenSource.Cancel();
            });

            await blockchainProcessor.ExecuteAsync(cancellationTokenSource.Token, minBlock);

            Assert.Single(processedBlockchainData.Blocks); // one block processed
            Assert.Equal(minBlock, processedBlockchainData.Blocks[0].Number.Value); // should have been the next block
            Assert.Equal(minBlock, await base.progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }

        [Fact]
        public async Task Will_Wait_For_Block_Confirmations_Before_Processing()
        {
            var blockLastProcessed = new BigInteger(100);
            var nextBlock = blockLastProcessed + 1;
            const uint minimumConfirmations = 10;
            //we have already processed block 100
            Initialise(lastBlockProcessed: blockLastProcessed, minimumBlockConfirmations: minimumConfirmations);
            //when first asked - pretend the current block is behind the required confirmations
            mockRpcResponses.MockGetBlockNumber(blockLastProcessed + minimumConfirmations);
            //the next time return an incremented block which is under the confirmation limit
            mockRpcResponses.MockGetBlockNumber(nextBlock + minimumConfirmations);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            mockRpcResponses.SetupTransactionsWithReceipts(blockNumber: nextBlock, numberOfTransactions: 2, logsPerTransaction: 2);

            var cancellationTokenSource = new CancellationTokenSource();

            //handle a block and then cancel
            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => {
                processedBlockchainData.Blocks.Add(block);
                cancellationTokenSource.Cancel();
            });

            await blockchainProcessor.ExecuteAsync(cancellationTokenSource.Token);

            Assert.Single(processedBlockchainData.Blocks); //should have processed a single block before cancellation
            Assert.Equal(2, mockRpcResponses.BlockNumberRequestCount); //should have asked for latest block twice
            Assert.Equal(1, base.WaitForBlockOccurances); // we should have waited once
            Assert.Equal(nextBlock, processedBlockchainData.Blocks[0].Number.Value); // should have handled the expected block
            Assert.Equal(nextBlock, await base.progressRepository.GetLastBlockNumberProcessedAsync()); // should have updated progress
        }
    }
}
