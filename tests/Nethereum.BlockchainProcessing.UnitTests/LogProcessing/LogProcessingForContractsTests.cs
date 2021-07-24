using Nethereum.BlockchainProcessing.UnitTests.TestUtils;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.LogProcessing
{
    public class LogProcessingForContractsTests : ProcessingTestBase
    {
        [Fact]
        public async Task Should_Configure_Get_Logs_Filter_For_Address_And_Event()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            const int logsPerTransaction = 1;
            const int transactionsPerBlock = 1;
            const string contractAddress1 = "0x243e72b69141f6af525a9a5fd939668ee9f2b354";
            const string contractAddress2 = "0x343e72b69141f6af525a9a5fd939668ee9f2b354";
            var contractAddresses = new[]{contractAddress1, contractAddress2 };

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyERC20Transfers(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var transfersHandled = new List<EventLog<TransferEventDTO>>();

            var logProcessor = Web3.Processing.Logs.CreateProcessorForContracts<TransferEventDTO>(
                contractAddresses,
                transferEventLog =>
                {
                    transfersHandled.Add(transferEventLog);
                });

            //act
            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            //assert
            var expectedGetLogsFilter = new FilterInputBuilder<TransferEventDTO>().Build(contractAddresses);
            var actualGetLogsFilter = logRpcMock.GetLogsFiltersInvoked.FirstOrDefault();
            var expectedLogCount = ((blockTo - blockFrom) + 1) * transactionsPerBlock * logsPerTransaction;

            Assert.Equal(expectedGetLogsFilter.Topics[0], actualGetLogsFilter.Topics[0]);
            Assert.Equal(expectedGetLogsFilter.Address, actualGetLogsFilter.Address);
            Assert.Equal(expectedLogCount, transfersHandled.Count);
        }

        [Fact]
        public async Task Should_Configure_Get_Logs_Filter_For_Address()
        {
            var currentBlockOnChain = 100;
            var blockFrom = new BigInteger(0);
            var blockTo = new BigInteger(0);
            const int logsPerTransaction = 1;
            const int transactionsPerBlock = 1;
            const string contractAddress1 = "0x243e72b69141f6af525a9a5fd939668ee9f2b354";
            const string contractAddress2 = "0x343e72b69141f6af525a9a5fd939668ee9f2b354";
            var contractAddresses = new[] { contractAddress1, contractAddress2 };

            //set up mock 
            var logRpcMock = new LogProcessingRpcMock(Web3Mock);
            logRpcMock.SetupGetCurrentBlockNumber(currentBlockOnChain);
            logRpcMock.SetupGetLogsToReturnDummyFilterLogs(blockFrom, blockTo, logsPerTransaction, transactionsPerBlock);

            var logsHandled = new List<FilterLog>();

            var logProcessor = Web3.Processing.Logs.CreateProcessorForContracts(
                contractAddresses,
                filterLog =>
                {
                    logsHandled.Add(filterLog);
                });

            //act
            await logProcessor.ExecuteAsync(blockTo, startAtBlockNumberIfNotProcessed: blockFrom);

            //assert
            var expectedLogCount = ((blockTo - blockFrom) + 1) * transactionsPerBlock * logsPerTransaction;

            var actualGetLogsFilter = logRpcMock.GetLogsFiltersInvoked.FirstOrDefault();
            Assert.Equal(contractAddresses, actualGetLogsFilter.Address);
            Assert.Equal(expectedLogCount, logsHandled.Count);
        }
    }
}
