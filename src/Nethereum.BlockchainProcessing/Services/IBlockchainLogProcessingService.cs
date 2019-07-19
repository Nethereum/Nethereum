using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services
{
    public interface IBlockchainLogProcessingService
    {
        BlockchainProcessor CreateProcessor<TEventDTO>(
            Action<EventLog<TEventDTO>> action,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Action<EventLog<TEventDTO>> action,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            string[] contractAddresses,
            Action<EventLog<TEventDTO>> action,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessor<TEventDTO>(
            Func<EventLog<TEventDTO>, Task> action,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Func<EventLog<TEventDTO>, Task> action,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            string[] contractAddresses,
            Func<EventLog<TEventDTO>, Task> action,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new();

        BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            ProcessorHandler<FilterLog> logProcessor,
            string[] contractAddresses,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class;

        BlockchainProcessor CreateProcessorForContract(

            string contractAddress,
            Action<FilterLog> action,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);

        BlockchainProcessor CreateProcessorForContracts(

            string[] contractAddresses,
            Action<FilterLog> action,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);

        BlockchainProcessor CreateProcessor(

            Action<FilterLog> action,
            Func<FilterLog, bool> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);

        BlockchainProcessor CreateProcessor(

            Func<FilterLog, Task> action,
            Func<FilterLog, Task<bool>> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);

        BlockchainProcessor CreateProcessor(

            ProcessorHandler<FilterLog> logProcessor,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);

        BlockchainProcessor CreateProcessor(

            IEnumerable<ProcessorHandler<FilterLog>> logProcessors,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null);
    }
}