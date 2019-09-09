using Moq;
using Nethereum.BlockchainProcessing.UnitTests.TestUtils;
using Nethereum.BlockchainProcessing.LogProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.LogProcessing
{
    public class LogOrchestratorTests
    {
        Web3Mock _web3Mock;
        LogOrchestrator _logOrchestrator;
        ProcessorHandler<FilterLog> _logHandler;
        List<FilterLog> _logsHandled;

        public LogOrchestratorTests()
        {
            _web3Mock = new Web3Mock();
            _logsHandled = new List<FilterLog>();
            _logHandler = new ProcessorHandler<FilterLog>((filterLog) => { _logsHandled.Add(filterLog); return Task.CompletedTask; });
            _logOrchestrator = new LogOrchestrator(_web3Mock.EthApiContractServiceMock.Object, new []{ _logHandler }, null, 100, 1);
        }

        [Fact]
        public void BlockRangeRequestStrategy_Is_Defaulted_When_Not_Explicity_Set()
        {
            _logOrchestrator = new LogOrchestrator(_web3Mock.EthApiContractServiceMock.Object, new[] { _logHandler }, null, 100, 1);
            var actualStrategy = _logOrchestrator.BlockRangeRequestStrategy as BlockRangeRequestStrategy;
            Assert.NotNull(actualStrategy);
            Assert.Equal(100, actualStrategy.DefaultNumberOfBlocksPerRequest);
            Assert.Equal(1, actualStrategy.RetryWeight);
        }

        [Fact]
        public void BlockRangeRequestStrategy_Can_Be_Injected()
        {
            var blockRangeRequestStrategy = new BlockRangeRequestStrategy();
            _logOrchestrator = new LogOrchestrator(_web3Mock.EthApiContractServiceMock.Object, new[] { _logHandler }, null, blockRangeRequestStrategy);
            Assert.Same(blockRangeRequestStrategy, _logOrchestrator.BlockRangeRequestStrategy);
        }


        [Fact]
        public async Task Should_Retrieve_Logs_And_Invoke_Handlers_And_Report_Last_Block_Processed()
        {
            var fromBlock = new BigInteger(10);
            var toBlock = new BigInteger(20);

            var logsRetrieved = new List<FilterLog>();

            _web3Mock.GetLogsMock
                .Setup(s => s.SendRequestAsync(It.IsAny<NewFilterInput>(), null))
                .Returns<NewFilterInput, object>((filter, id) =>
                {
                    var logs = new[] { new FilterLog() };
                    logsRetrieved.AddRange(logs);
                    return Task.FromResult(logs);
                });


            var progress = await _logOrchestrator.ProcessAsync(fromBlock, toBlock);

            Assert.NotNull(progress);
            Assert.Equal(toBlock, progress.BlockNumberProcessTo);
            Assert.Null(progress.Exception);
            Assert.Equal(logsRetrieved, _logsHandled);
        }

        [Fact]
        public async Task When_Log_Retrieval_Fails_Should_Retry()
        {
            var fromBlock = new BigInteger(10);
            var toBlock = new BigInteger(20);

            var logsRetrieved = new List<FilterLog>();

            _web3Mock.GetLogsMock
                .Setup(s => s.SendRequestAsync(It.IsAny<NewFilterInput>(), null))
                .Returns<NewFilterInput, object>((filter, id) =>
                {
                    if(filter.NumberOfBlocksInBlockParameters() > 5) throw new Exception("fake too many records exception");

                    var logs = new[] { new FilterLog() };
                    logsRetrieved.AddRange(logs);
                    return Task.FromResult(logs);
                });


            var progress = await _logOrchestrator.ProcessAsync(fromBlock, toBlock);

            Assert.NotNull(progress);
            Assert.Equal(toBlock, progress.BlockNumberProcessTo);
            Assert.Null(progress.Exception);
            Assert.Equal(logsRetrieved, _logsHandled);
        }

        [Fact]
        public async Task When_Max_Log_Retrieval_RetryAttempt_Is_Exceeded_Returns_ProgressWithException()
        {
            var fromBlock = new BigInteger(10);
            var toBlock = new BigInteger(20);

            var retrievalException = new Exception("fake retrieval exception");

            var calls = 0;
            //set up to throw every time
            _web3Mock.GetLogsMock
                .Setup(s => s.SendRequestAsync(It.IsAny<NewFilterInput>(), null))
                .Callback(() => calls++)
                .ThrowsAsync(retrievalException);

            var progress = await _logOrchestrator.ProcessAsync(fromBlock, toBlock);

            var retries = calls - 1; // minus first call
            Assert.Equal(retrievalException, progress.Exception);
            Assert.Equal(LogOrchestrator.MaxGetLogsRetries, retries);
            Assert.Empty(_logsHandled);
        }
    }
}
