using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.LogProcessing
{
    public class LogProcessingTests : ProcessingTestBase
    {
        [Fact]
        public async Task Should_Crawl_Block_Range_And_Invoke_Handlers()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(10);
            var blockTo = new BigInteger(20);
            await ProcessRangeAndAssert(currentBlockOnChain, blockFrom, blockTo);
        }

        [Fact]
        public async Task Should_Cope_With_Starting_At_Block_0()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);

            await ProcessRangeAndAssert(currentBlockOnChain, blockFrom, blockTo);
        }


        private async Task ProcessRangeAndAssert(int currentBlockOnChain, BigInteger blockFrom, BigInteger blockTo)
        {
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);

            var blocksInRange = (blockTo - blockFrom) + 1;

            //set up mock 
            var logsPerTransaction = 10;
            var transactionsPerBlock = 10;
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);

            for (var block = blockFrom; block <= blockTo; block++)
            {
                logRpcMock.SetupLogsToReturn(block, transactionsPerBlock, logsPerTransaction);
            }

            var logsExpected = blocksInRange * (transactionsPerBlock * logsPerTransaction);

            var logsHandled = new List<FilterLog>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor(filterLog => logsHandled.Add(filterLog));

            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            Assert.Equal(logsExpected, logsHandled.Count);
            Assert.Equal(1, logRpcMock.GetLogRequestCount);
        }


        [Fact]
        public async Task Retries_Log_Retrieval_On_Error()
        {
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);

            //parameters
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(1);
            var blocksInRange = (blockTo - blockFrom) + 1;

            //set up mock 
            var logsPerTransaction = 10;
            var transactionsPerBlock = 10;
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);

            for (var block = blockFrom; block <= blockTo; block++)
            {
                logRpcMock.SetupLogsToReturn(block, transactionsPerBlock, logsPerTransaction);
            }

            var logsExpected = blocksInRange * (transactionsPerBlock * logsPerTransaction);

            var logsHandled = new List<FilterLog>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor(filterLog => logsHandled.Add(filterLog));

            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            Assert.Equal(logsExpected, logsHandled.Count);
            Assert.Equal(1, logRpcMock.GetLogRequestCount);

        }
    }
}
