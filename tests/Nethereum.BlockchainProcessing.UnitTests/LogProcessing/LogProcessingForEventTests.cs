using Nethereum.BlockchainProcessing.UnitTests.TestUtils;
using Nethereum.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.LogProcessing
{
    public class LogProcessingForEventTests : ProcessingTestBase
    {
        [Fact]
        public async Task Should_Match_Event_And_Invoke_Handler()
        {
            //setup
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            var blocksInRange = (blockTo - blockFrom) + 1;
            const int logsPerTransaction = 10;
            const int transactionsPerBlock = 10;

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyERC20Transfers(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var transfersHandled = new List<EventLog<TransferEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
                transferEvent => transfersHandled.Add(transferEvent));

            //act
            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            //assert
            var logsExpected = blocksInRange * (transactionsPerBlock * logsPerTransaction);
            Assert.Equal(logsExpected, transfersHandled.Count);
        }

        [Fact]
        public async Task Should_Not_Match_Other_Events()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            const int logsPerTransaction = 10;
            const int transactionsPerBlock = 10;

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyERC20Transfers(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var approvalsHandled = new List<EventLog<ApprovalEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor<ApprovalEventDTO>(
                approvalEvent => approvalsHandled.Add(approvalEvent));

            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            Assert.Empty(approvalsHandled);
        }

        [Fact]
        public async Task Should_Accept_Event_Specific_Criteria()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            var blocksInRange = (blockTo - blockFrom) + 1;
            const int logsPerTransaction = 10;
            const int transactionsPerBlock = 10;

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyERC20Transfers(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var transfersHandled = new List<EventLog<TransferEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
                //action
                action: transferEventLog => { 
                    transfersHandled.Add(transferEventLog); return Task.CompletedTask; }, 
                //criteria
                criteria: transferEventLog => {
                    var match = transferEventLog.Event.Value > 9999999999999;
                    return Task.FromResult(match);
                    });

            //act
            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            //assert
            Assert.Empty(transfersHandled);
        }

        [Fact]
        public async Task Get_Logs_Request_Will_Filter_Specifically_For_This_Event()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            const int logsPerTransaction = 1;
            const int transactionsPerBlock = 1;

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyERC20Transfers(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var transfersHandled = new List<EventLog<TransferEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
                transferEventLog =>
                {
                    transfersHandled.Add(transferEventLog); return Task.CompletedTask;
                });

            //act
            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            //assert
            var expectedGetLogsFilter = new FilterInputBuilder<TransferEventDTO>().Build();
            var actualGetLogsFilter = logRpcMock.GetLogsFiltersInvoked.FirstOrDefault();

            Assert.Equal(expectedGetLogsFilter.Topics[0], actualGetLogsFilter.Topics[0]);
        }

    }

}
