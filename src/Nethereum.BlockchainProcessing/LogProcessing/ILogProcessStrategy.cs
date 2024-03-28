using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public interface ILogProcessStrategy
    {
        Task ProcessLogs(FilterLog[] logs, IEnumerable<ProcessorHandler<FilterLog>> logProcessors);
    }
}