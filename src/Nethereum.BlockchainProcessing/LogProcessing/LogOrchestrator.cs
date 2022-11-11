using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
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
        private BlockRangeRequestStrategy _blockRangeRequestStrategy;

        protected IEthApiContractService EthApi { get; set; }

        public LogOrchestrator(IEthApiContractService ethApi,
            IEnumerable<ProcessorHandler<FilterLog>> logProcessors, NewFilterInput filterInput = null, int defaultNumberOfBlocksPerRequest = 100, int retryWeight = 0)
        {
            EthApi = ethApi;
            _logProcessors = logProcessors;
            _filterInput = filterInput ?? new NewFilterInput();
            _blockRangeRequestStrategy = new BlockRangeRequestStrategy(defaultNumberOfBlocksPerRequest, retryWeight);
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber,
            CancellationToken cancellationToken = default(CancellationToken), IBlockProgressRepository blockProgressRepository = null)
        {
            var progress = new OrchestrationProgress();
            var nextBlockNumberFrom = fromNumber;
            try
            {
                while (!progress.HasErrored && progress.BlockNumberProcessTo != toNumber && !cancellationToken.IsCancellationRequested)
                {
                    if (progress.BlockNumberProcessTo != null)
                    {
                        nextBlockNumberFrom = progress.BlockNumberProcessTo.Value + 1;
                    }

                    var getLogsResponse = await GetLogsAsync(progress, nextBlockNumberFrom, toNumber).ConfigureAwait(false);

                    if (getLogsResponse == null || cancellationToken.IsCancellationRequested) return progress; //allowing all the logs to be processed if not cancelled before hand

                    var logs = getLogsResponse.Value.Logs;

                    if (logs != null)
                    {
                        logs = logs.Sort();
                        await InvokeLogProcessorsAsync(logs).ConfigureAwait(false);
                    }
                    progress.BlockNumberProcessTo = getLogsResponse.Value.To;
                    if (blockProgressRepository != null)
                    {
                        await blockProgressRepository.UpsertProgressAsync(progress.BlockNumberProcessTo.Value).ConfigureAwait(false);
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

        private async Task<GetLogsResponse?> GetLogsAsync(OrchestrationProgress progress, BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default(CancellationToken), int retryRequestNumber = 0, int retryNullLogsRequestNumber = 0)
        {
            try 
            {


                if (cancellationToken.IsCancellationRequested) return null; // check cancellation on entry as this is recursive

                var adjustedToBlock =
                    _blockRangeRequestStrategy.GeBlockNumberToRequestTo(fromBlock, toBlock,
                        retryRequestNumber);

                _filterInput.SetBlockRange(fromBlock, adjustedToBlock);

                var logs = await EthApi.Filters.GetLogs.SendRequestAsync(_filterInput).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return null; // check cancellation after logs as this might be a long call

                //If we don't get any, lets retry in case there is an issue with the node.
                if (logs == null && retryNullLogsRequestNumber < MaxGetLogsNullRetries)
                {
                    return await GetLogsAsync(progress, fromBlock, toBlock, cancellationToken, 0, retryNullLogsRequestNumber + 1).ConfigureAwait(false);
                }
                retryRequestNumber = 0;
                retryNullLogsRequestNumber = 0;
                return new GetLogsResponse(fromBlock, adjustedToBlock, logs);

            }
            catch(Exception ex)
            {
                
                if (retryRequestNumber >= MaxGetLogsRetries || cancellationToken.IsCancellationRequested)
                {
                    progress.Exception = ex;
                    return null;
                }
                else
                {
                    return await GetLogsAsync(progress, fromBlock, toBlock, cancellationToken, retryRequestNumber + 1).ConfigureAwait(false);
                }
            }
        }

    }
}
