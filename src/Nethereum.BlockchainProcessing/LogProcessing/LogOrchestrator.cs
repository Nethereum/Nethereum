using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public class LogOrchestrator : IBlockchainProcessingOrchestrator
    {
        private readonly IEnumerable<ProcessorHandler<FilterLog>> _logProcessors;
        private NewFilterInput _filterInput;
        private BlockRangeRequestStrategy _blockRangeRequestStrategy;
        protected IEthApiContractService EthApi { get; set; }

        public LogOrchestrator(IEthApiContractService ethApi,
            IEnumerable<ProcessorHandler<FilterLog>> logProcessors, NewFilterInput filterInput = null, int defaultNumberOfBlocksPerRequest = 100)
        {
            EthApi = ethApi;
            _logProcessors = logProcessors;
            _filterInput = filterInput ?? new NewFilterInput();
            _blockRangeRequestStrategy = new BlockRangeRequestStrategy(defaultNumberOfBlocksPerRequest);
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var progress = new OrchestrationProgress();
            var retryRequestNumber = 0;
            var maxRetries = 4;
            var nextBlockNumberFrom = fromNumber;

            while (!progress.HasErrored && progress.BlockNumberProcessTo != toNumber)
            {
                try
                {

                    if (progress.BlockNumberProcessTo != null)
                    {
                        nextBlockNumberFrom = progress.BlockNumberProcessTo.Value + 1;
                    }

                    var nextBlockNumberTo =
                        _blockRangeRequestStrategy.GeBlockNumberToRequestTo(nextBlockNumberFrom - 1, toNumber,
                            retryRequestNumber);

                    _filterInput.FromBlock = new BlockParameter(nextBlockNumberFrom.ToHexBigInteger());
                    _filterInput.ToBlock = new BlockParameter(nextBlockNumberTo.ToHexBigInteger());

                    var logs = await EthApi.Filters.GetLogs.SendRequestAsync(_filterInput);

                    if (logs == null) return progress;

                    if (!cancellationToken.IsCancellationRequested) //allowing all the logs to be processed if not cancelled before hand
                    {

                        logs = logs.Sort();

                        //TODO: Add parallel execution strategy
                        foreach (var logProcessor in _logProcessors)
                        {
                            foreach (var log in logs)
                            {
                                await logProcessor.ExecuteAsync(log);
                            }
                        }

                        progress.BlockNumberProcessTo = nextBlockNumberTo;
                    }
                
                }
                catch (Exception ex)
                {
                    //TODO ADD better logic for retries 
                    if (retryRequestNumber > maxRetries)
                    {
                        progress.Exception = ex;
                    }
                    else
                    {
                        retryRequestNumber = retryRequestNumber + 1;
                    }
                }
            }
            return progress;

        }

    }
}
