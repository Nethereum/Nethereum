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
        protected IEthApiContractService EthApi { get; set; }

        public LogOrchestrator(IEthApiContractService ethApi,
            IEnumerable<ProcessorHandler<FilterLog>> logProcessors, NewFilterInput filterInput = null)
        {
            EthApi = ethApi;
            _logProcessors = logProcessors;
            _filterInput = filterInput ?? new NewFilterInput();
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber, CancellationToken cancellationToken)
        {
            var progress = new OrchestrationProgress();
            try
            {
                _filterInput.FromBlock = new BlockParameter(fromNumber.ToHexBigInteger());
                _filterInput.ToBlock = new BlockParameter(toNumber.ToHexBigInteger());
                //TODO: add retry strategy on too many records.
                var logs = await EthApi.Filters.GetLogs.SendRequestAsync(_filterInput);

                if (logs == null) return progress;

                if (!cancellationToken.IsCancellationRequested) //allowing all the logs to be processed if not cancelled before hand
                { 
                    
                    logs = logs.Sort();

                    //TODO: Add paralell execution strategy
                    foreach (var logProcessor in _logProcessors)
                    {
                        foreach (var log in logs)
                        {
                            await logProcessor.ExecuteAsync(log);
                        }
                    }

                    progress.BlockNumberProcessTo = toNumber;
                }
            }
            catch (Exception ex)
            {
                progress.Exception = ex;
            }

            return progress;

        }
    }
}
