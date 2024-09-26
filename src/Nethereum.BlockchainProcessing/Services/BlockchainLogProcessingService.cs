using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.LogProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services
{
    public class BlockchainLogProcessingService : IBlockchainLogProcessingService
    {
        public const int RetryWeight = 50;
        public const int DefaultNumberOfBlocksPerRequest = 1000000;

        private readonly IEthApiContractService _ethApiContractService;

        public BlockchainLogProcessingService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
            ERC20 = new ERC20LogProcessingService(this, ethApiContractService);
            ERC721 = new ERC721LogProcessingService(this, ethApiContractService);
        }

        public IERC20LogProcessingService ERC20 { get; private set; }
        public IERC721LogProcessingService ERC721 { get; private set; }

        public async Task<List<FilterLog>> GetAllEvents(NewFilterInput filterInput,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
            int retryWeight = RetryWeight)
        {
            var returnEventLogs = new List<FilterLog>();

            void StoreLog(FilterLog eventLog)
            {
                returnEventLogs.Add(eventLog);
            }

            Func<FilterLog, Task<bool>> criteria = null;

            var fromBlock = fromBlockNumber ?? 0;
            var blockProgressRepository = new InMemoryBlockchainProgressRepository(fromBlock);

            var processor = CreateProcessor(
                new[] { new ProcessorHandler<FilterLog>(StoreLog, criteria) },
                minimumBlockConfirmations: 0,
                filterInput, blockProgressRepository, null, numberOfBlocksPerRequest, retryWeight);


            if (toBlockNumber == null)
            {
                var currentBlockNumber = await _ethApiContractService.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
                toBlockNumber = currentBlockNumber.Value;
            }

            await processor.ExecuteAsync(
                cancellationToken: cancellationToken, toBlockNumber: toBlockNumber.Value).ConfigureAwait(false);

            return returnEventLogs;
        }

        public Task<List<FilterLog>> GetAllEventsForContracts(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
            int retryWeight = RetryWeight) 
        {
            return GetAllEvents(new NewFilterInput() { Address = contractAddresses }, fromBlockNumber,
                toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public Task<List<FilterLog>> GetAllEventsForContract(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
            int retryWeight = RetryWeight) 
        {
            return GetAllEventsForContracts(contractAddresses: new[] { contractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }


        public async Task<List<EventLog<TEventDTO>>> GetAllEvents<TEventDTO>(NewFilterInput filterInput,
    BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
    int retryWeight = RetryWeight) where TEventDTO : class, new()
        {
            var returnEventLogs = new List<EventLog<TEventDTO>>();

            Task StoreLogAsync(EventLog<TEventDTO> eventLog)
            {
                returnEventLogs.Add(eventLog);
#if !NETSTANDARD1_1 && !NET451
                return Task.CompletedTask;
#else
                return Task.FromResult(0);
#endif
            }

            var fromBlock = fromBlockNumber ?? 0;
            var blockProgressRepository = new InMemoryBlockchainProgressRepository(fromBlock);

            var processor = CreateProcessor(new[]
                {
                    new EventLogProcessorHandler<TEventDTO>(StoreLogAsync, null)
                },
                minimumBlockConfirmations: 0,
                filterInput, blockProgressRepository, null, numberOfBlocksPerRequest, retryWeight);


            if (toBlockNumber == null)
            {
                var currentBlockNumber = await _ethApiContractService.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
                toBlockNumber = currentBlockNumber.Value;
            }

            await processor.ExecuteAsync(
                cancellationToken: cancellationToken, toBlockNumber: toBlockNumber.Value).ConfigureAwait(false);

            return returnEventLogs;
        }


        public Task<List<EventLog<TEventDTO>>> GetAllEventsForContracts<TEventDTO>(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
            int retryWeight = RetryWeight) where TEventDTO : class, new()
        {
            var filter = ABITypedRegistry.GetEvent<TEventDTO>().CreateFilterInput();
            filter.Address = contractAddresses;
            return GetAllEvents<TEventDTO>(filter, fromBlockNumber,
                toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public Task<List<EventLog<TEventDTO>>> GetAllEventsForContract<TEventDTO>(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest,
            int retryWeight = RetryWeight) where TEventDTO : class, new()
        {
            return GetAllEventsForContracts<TEventDTO>( contractAddresses: new[] { contractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public BlockchainProcessor CreateProcessor<TEventDTO>(
            Action<EventLog<TEventDTO>> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(), blockProgressRepository, log);


        public BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Action<EventLog<TEventDTO>> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class, new() =>
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
            ILogger log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(contractAddresses), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessor<TEventDTO>(
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(), blockProgressRepository, log);


        public BlockchainProcessor CreateProcessorForContract<TEventDTO>(
            string contractAddress,
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(new[] {contractAddress}), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            string[] contractAddresses,
            Func<EventLog<TEventDTO>, Task> action,
            uint minimumBlockConfirmations,
            Func<EventLog<TEventDTO>, Task<bool>> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class, new() =>
            CreateProcessor(new[] {new EventLogProcessorHandler<TEventDTO>(action, criteria)}, minimumBlockConfirmations,
                new FilterInputBuilder<TEventDTO>().Build(contractAddresses), blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts<TEventDTO>(
            ProcessorHandler<FilterLog> logProcessor,
            string[] contractAddresses,
            uint minimumBlockConfirmations,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) where TEventDTO : class =>
            CreateProcessor(new[] {logProcessor}, minimumBlockConfirmations, new FilterInputBuilder<TEventDTO>().Build(contractAddresses),
                blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContract(

            string contractAddress,
            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations,
            new NewFilterInput {Address = new[] {contractAddress}}, blockProgressRepository, log);

        public BlockchainProcessor CreateProcessorForContracts(

            string[] contractAddresses,
            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations,
            new NewFilterInput {Address = contractAddresses}, blockProgressRepository, log);


        //sync action and criter
        public BlockchainProcessor CreateProcessor(

            Action<FilterLog> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, bool> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations, filter,
            blockProgressRepository, log);

        //async action and criteria
        public BlockchainProcessor CreateProcessor(

            Func<FilterLog, Task> action,
            uint minimumBlockConfirmations,
            Func<FilterLog, Task<bool>> criteria = null,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) => CreateProcessor(new[] {new ProcessorHandler<FilterLog>(action, criteria)}, minimumBlockConfirmations, filter,
            blockProgressRepository, log);

        //single processor
        public BlockchainProcessor CreateProcessor(

            ProcessorHandler<FilterLog> logProcessor,
            uint minimumBlockConfirmations,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null) => CreateProcessor(new[] {logProcessor}, minimumBlockConfirmations, filter, blockProgressRepository, log);

        //multi processor
        public BlockchainProcessor CreateProcessor(

            IEnumerable<ProcessorHandler<FilterLog>> logProcessors,
            uint minimumBlockConfirmations,
            NewFilterInput filter = null,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null, int defaultNumberOfBlocksPerRequest = 100, int retryWeight = 0)
        {
            var orchestrator = new LogOrchestrator(_ethApiContractService, logProcessors, filter, defaultNumberOfBlocksPerRequest, retryWeight, log);

            var progressRepository = blockProgressRepository ??
                                     new InMemoryBlockchainProgressRepository();
            var lastConfirmedBlockNumberService =
                new LastConfirmedBlockNumberService(_ethApiContractService.Blocks.GetBlockNumber, minimumBlockConfirmations);

            return new BlockchainProcessor(orchestrator, progressRepository, lastConfirmedBlockNumberService, log);
        }

    }
}