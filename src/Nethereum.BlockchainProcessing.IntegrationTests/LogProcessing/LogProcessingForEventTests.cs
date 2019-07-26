using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.LogProcessing
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
            SetupGetLogsToReturnDummyTransfers(blockFrom, blockTo, logRpcMock, logsPerTransaction, transactionsPerBlock);

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
            SetupGetLogsToReturnDummyTransfers(blockFrom, blockTo, logRpcMock, logsPerTransaction, transactionsPerBlock);

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
            SetupGetLogsToReturnDummyTransfers(blockFrom, blockTo, logRpcMock, logsPerTransaction, transactionsPerBlock);

            var transfersHandled = new List<EventLog<TransferEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
                //action
                transferEventLog => { 
                    transfersHandled.Add(transferEventLog); return Task.CompletedTask; }, 
                //criteria
                transferEventLog => {
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
            SetupGetLogsToReturnDummyTransfers(blockFrom, blockTo, logRpcMock, logsPerTransaction, transactionsPerBlock);

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

        private static void SetupGetLogsToReturnDummyTransfers(BigInteger blockFrom, BigInteger blockTo, LogProcessingRpcMock logRpcMock, int logsPerTransaction, int transactionsPerBlock)
        {
            for (var block = blockFrom; block <= blockTo; block++)
            {
                logRpcMock.SetupLogsToReturn(
                    block,
                    transactionsPerBlock,
                    logsPerTransaction,
                    (bNum, txIndex, logIndex) =>
                    CreateDummyTransfer(bNum, txIndex, logIndex));
            }
        }

        private static FilterLog CreateDummyTransfer(BigInteger blockNumber, BigInteger transactionIndex, BigInteger logIndex)
        {
            var sample = SampleTransferLog();
            sample.BlockNumber = blockNumber.ToHexBigInteger();
            sample.TransactionIndex = transactionIndex.ToHexBigInteger();
            sample.LogIndex = logIndex.ToHexBigInteger();
            return sample;
        }

        private static FilterLog SampleTransferLog()
        {
            return SampleTransferLogAsJObject.ToObject<FilterLog>();
        }

        private static readonly JObject SampleTransferLogAsJObject = JObject.Parse(
    $@"{{
  'address': '0x243e72b69141f6af525a9a5fd939668ee9f2b354',
  'topics': [
    '0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef',
    '0x00000000000000000000000012890d2cce102216644c59dae5baed380d84830c',
    '0x00000000000000000000000013f022d72158410433cbd66f5dd8bf6d2d129924'
  ],
  'data': '0x00000000000000000000000000000000000000000000000000000000000003e8',
  'blockNumber': '0x36',
  'transactionHash': '0x19ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd',
  'transactionIndex': '0x0',
  'blockHash': '0x58dab5a71037752b36e0a6af02f290fbc3dc5b2abf88d88f2c04defd9b8fb03b',
  'logIndex': '0x0',
  'removed': false
}}");
    }
}
