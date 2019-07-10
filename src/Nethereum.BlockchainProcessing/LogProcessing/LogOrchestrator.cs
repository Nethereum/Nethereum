using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    //public class LogOrchestrator : IBlockchainProcessingOrchestrator
    //{
    //    protected IEthApiContractService EthApi { get; set; }

    //    public BlockCrawlOrchestrator(IEthApiContractService ethApi,
    //        IEnumerable<BlockchainProcessorExecutionSteps> executionStepsCollection)
    //    {

    //    }

    //    public Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber)
    //    {
    //        var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(new NewFilterInput
    //        {
    //            FromBlock = new BlockParameter(fromNumber),
    //            ToBlock = new BlockParameter(toNumber)
    //        });

    //        if (logs == null) return;

    //        var processingCollection = new List<LogsMatchedForProcessing>();

    //        foreach (var logProcessor in _logProcessors)
    //        {
    //            processingCollection.Add(new LogsMatchedForProcessing(logProcessor));
    //        }

    //        foreach (var log in logs)
    //        {
    //            foreach (var matchedForProcessing in processingCollection)
    //            {
    //                matchedForProcessing.AddIfMatched(log);
    //            }
    //        }

    //        foreach (var matchedForProcessing in processingCollection)
    //        {
    //            await matchedForProcessing.ProcessLogsAsync();
    //        }
    //    }
    //}
}
