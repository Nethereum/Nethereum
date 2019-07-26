using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.BlockchainProcessing.LogProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services
{
    public class BlockchainLogProcessingService : IBlockchainLogProcessingService
    {
        private readonly IEthApiContractService _ethApiContractService;

        public BlockchainLogProcessingService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public BlockchainProcessor CreateProcessor<TEventDTO>(
            Action<EventLog<TEventDTO>> action,
            uint minimumBlockConfirmations,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(), blockProgressRepository, log);


        public BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Action<EventLog<TEventDTO>> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[]
            {
                new EventLogProcessorHandler<TEventDTO>(action, criteria)
            },
                minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(new[]
            {
                contractAddress
            }), blockProgressRepository, log);


        public BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            string[] contractAddresses,
            Action<EventLog<TEventDTO>> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(contractAddresses), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessor<TEventDTO>(
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(), blockProgressRepository, log);


        public BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(new[] {contractAddress}), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            string[] contractAddresses,
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(contractAddresses), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            ProcessorHandler<FilterLog> logProcessor,
            string[] contractAddresses,
            uint minimumBlockConfirmations,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) where TEventDTO : class =>
            CreateProcessor(new[] {logProcessor}, minimumBlockConfirmations, new FilterInputBuilder<TEventDTO>().Build(contractAddresses),
                blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContract(

            string contractAddress,
            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations,
            new NewFilterInput {Address = new[] {contractAddress}}, blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts(

            string[] contractAddresses,
            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations,
            new NewFilterInput {Address = contractAddresses}, blockProgressRepository, log);


        //sync action and criter
        public BlockchainProcessor CreateProcessor(

            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations, filter,
            blockProgressRepository, log);

        //async action and criteria
        public BlockchainProcessor CreateProcessor(

            Func<FilterLog, Task> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, Task<bool>> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations, filter,
            blockProgressRepository, log);

        //single processor
        public BlockchainProcessor CreateProcessor(

            ProcessorHandler<FilterLog> logProcessor,
            uint minimumBlockConfirmations,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null) => CreateProcessor(new[] {logProcessor}, minimumBlockConfirmations, filter, blockProgressRepository, log);

        //multi processor
        public BlockchainProcessor CreateProcessor(

            IEnumerable<ProcessorHandler<FilterLog>> logProcessors,
            uint minimumBlockConfirmations,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILog log = null)
        {
            var orchestrator = new LogOrchestrator(_ethApiContractService, logProcessors, filter);

            var progressRepository = blockProgressRepository ??
                                     new InMemoryBlockchainProgressRepository();
            var lastConfirmedBlockNumberService =
                new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber, minimumBlockConfirmations);

            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService, log);
        }

    }
}