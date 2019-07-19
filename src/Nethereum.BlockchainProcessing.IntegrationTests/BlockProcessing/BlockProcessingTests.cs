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
    public class BlockProcessingTests
    {
        public class RpcResponses
        {
            public List<BlockWithTransactions> Blocks { get;set;} = new List<BlockWithTransactions>();

            public List<TransactionReceipt> Receipts { get;set;} = new List<TransactionReceipt>();

        }

        [Fact]
        public async Task ShouldCrawlBlocksAndInvokeHandlingSteps()
        {
            var web3Mock = new Web3Mock();
            var processingSteps = new BlockProcessingSteps();
            var orchestrator = new BlockCrawlOrchestrator(web3Mock.EthApiContractServiceMock.Object, new []{ processingSteps });
            var progressRepository = new InMemoryBlockchainProgressRepository(lastBlockProcessed: null);
            var lastConfirmedBlockService = new LastConfirmedBlockNumberService(web3Mock.BlockNumberMock.Object);
            var blockchainProcessor = new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockService);

            var mockRpcResponses = new RpcResponses();

            var currentBlockOnChain = new HexBigInteger(100);

            // 0x243e72b69141f6af525a9a5fd939668ee9f2b354

            mockRpcResponses.Blocks.Add(new BlockWithTransactions
            {
                Number = new HexBigInteger(1),
                Transactions = new[]
                {
                    new Transaction{TransactionHash = "0x10ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd", TransactionIndex = new HexBigInteger(1)},
                    new Transaction{TransactionHash = "0x20ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd", TransactionIndex = new HexBigInteger(2)}
                }
            });

            mockRpcResponses.Receipts.AddRange(new []
            {
                new TransactionReceipt { TransactionHash = "0x10ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd", Logs = new []{ new FilterLog() }.ConvertToJArray() },
                new TransactionReceipt { TransactionHash = "0x20ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd", Logs = new []{ new FilterLog() }.ConvertToJArray() } 
            });

            web3Mock.BlockNumberMock.Setup(m => m.SendRequestAsync(null)).ReturnsAsync(currentBlockOnChain);

            web3Mock.GetBlockWithTransactionsByNumberMock.Setup(s => s.SendRequestAsync(It.IsAny<HexBigInteger>(), null))
                .Returns<HexBigInteger, object>((n, id) =>
                {
                    return Task.FromResult(mockRpcResponses.Blocks.FirstOrDefault(b => b.Number == n));
                });

            web3Mock.GetTransactionReceiptMock.Setup(s => s.SendRequestAsync(It.IsAny<string>(), null))
                .Returns<string, object>((hash, id) =>
                {
                    return Task.FromResult(mockRpcResponses.Receipts.FirstOrDefault(b => b.TransactionHash == hash));
                });


            var blocks = new List<BlockWithTransactions>();
            var transactions = new List<TransactionVO>();
            var transactionsWithReceipt = new List<TransactionReceiptVO>();
            var contractCreations = new List<ContractCreationVO>();
            var filterLogs = new List<FilterLogVO>();

            processingSteps.BlockStep.AddSynchronousProcessorHandler((block) => blocks.Add(block));
            processingSteps.TransactionStep.AddSynchronousProcessorHandler((tx) => transactions.Add(tx));
            processingSteps.TransactionReceiptStep.AddSynchronousProcessorHandler((tx) => transactionsWithReceipt.Add(tx));
            processingSteps.ContractCreationStep.AddSynchronousProcessorHandler((c) => contractCreations.Add(c));
            processingSteps.FilterLogStep.AddSynchronousProcessorHandler((filterLog) => filterLogs.Add(filterLog));

            var fromBlock = new BigInteger(1);
            var toBlock = fromBlock;

            var cancellationTokenSource = new CancellationTokenSource();

            var progress =  await blockchainProcessor.ExecuteAsync(toBlock, cancellationTokenSource.Token, fromBlock);


        }
    }
}
