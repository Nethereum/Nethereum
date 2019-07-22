using Nethereum.BlockchainProcessing.Processor;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{


    public class BlockProcessingTests : BlockProcessingTestBase
    {

        [Fact]
        public async Task ShouldCrawlBlocksAndInvokeHandlingSteps()
        {
            Initialise(lastBlockProcessed: null);
            MockGetBlockNumber(100);

            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            mockRpcResponses.AddTransactionsWithReceipts(blockNumber: 1, numberOfTransactions: 2, logsPerTransaction: 2);

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


        }
    }
}
