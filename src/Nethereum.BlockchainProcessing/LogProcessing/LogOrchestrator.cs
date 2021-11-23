using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public class LogOrchestrator : IBlockchainProcessingOrchestrator
    {
        public const int MaxGetLogsRetries = 10;
        public const int MaxGetLogsNullRetries = 1;

        private readonly IEnumerable<ProcessorHandler<FilterLog>> _logProcessors;
        private NewFilterInput _filterInput;

        protected IEthApiContractService EthApi { get; set; }

        public LogOrchestrator(IEthApiContractService ethApi,
            IEnumerable<ProcessorHandler<FilterLog>> logProcessors, NewFilterInput filterInput = null)
        {
            EthApi = ethApi;
            _logProcessors = logProcessors;
            _filterInput = filterInput ?? new NewFilterInput();
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var progress = new OrchestrationProgress();
            var nextBlockNumberFrom = fromNumber;
            try
            {
                while (!progress.HasErrored && progress.BlockNumberProcessTo != toNumber)
                {
                    if (progress.BlockNumberProcessTo != null)
                    {
                        nextBlockNumberFrom = progress.BlockNumberProcessTo.Value + 1;
                    }

                    var getLogsResponse = await GetLogsAsync(progress, nextBlockNumberFrom, toNumber).ConfigureAwait(false);

                    if (getLogsResponse == null) return progress;

                    var logs = getLogsResponse.Value.Logs;

                    if (!cancellationToken.IsCancellationRequested) //allowing all the logs to be processed if not cancelled before hand
                    {
                        if (logs != null)
                        {
                            logs = logs.Sort();
                            await InvokeLogProcessorsAsync(logs).ConfigureAwait(false);
                        }
                        progress.BlockNumberProcessTo = getLogsResponse.Value.To;
                    }
                }
            }
            catch(Exception ex)
            {
                progress.Exception = ex;
            }
            return progress;

        }

        private async Task InvokeLogProcessorsAsync(FilterLog[] logs)
        {
            //TODO: Add parallel execution strategy
            foreach (var logProcessor in _logProcessors)
            {
                foreach (var log in logs)
                {
                    await logProcessor.ExecuteAsync(log).ConfigureAwait(false);
                }
            }
        }

        struct GetLogsResponse
        {
            public GetLogsResponse(BigInteger from, BigInteger to, FilterLog[] logs)
            {
                Logs = logs;
                From = from;
                To = to;
            }

            public FilterLog[] Logs { get;set;}
            public BigInteger From { get; set; }
            public BigInteger To { get; set;}
        }

        private async Task<GetLogsResponse?> GetLogsAsync(OrchestrationProgress progress, BigInteger fromBlock, BigInteger toBlock, int retryRequestNumber = 0, int retryNullLogsRequestNumber = 0)
        {
            try 
            {
                toBlock -= retryRequestNumber;
                _filterInput.SetBlockRange(fromBlock, toBlock);

                var logs = await EthApi.Filters.GetLogs.SendRequestAsync(_filterInput).ConfigureAwait(false);

                //If we don't get any, lets retry in case there is an issue with the node.
                if(logs == null && retryNullLogsRequestNumber < MaxGetLogsNullRetries)
                {
                    return await GetLogsAsync(progress, fromBlock, toBlock, 0, retryNullLogsRequestNumber + 1).ConfigureAwait(false);
                }
                retryRequestNumber = 0;
                retryNullLogsRequestNumber = 0;
                return new GetLogsResponse(fromBlock, toBlock, logs);

            }
            catch(Exception ex)
            {
                if (retryRequestNumber >= MaxGetLogsRetries)
                {
                    progress.Exception = ex;
                    return null;
                }
                else
                {
                    return await GetLogsAsync(progress, fromBlock, toBlock, retryRequestNumber + 1).ConfigureAwait(false);
                }
            }
        }

    }
}
