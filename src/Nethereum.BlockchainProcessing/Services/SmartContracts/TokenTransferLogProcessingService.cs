using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;
using ERC721Transfer = Nethereum.Contracts.Standards.ERC721.ContractDefinition.TransferEventDTO;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public class TokenTransferLogProcessingService
    {
        protected readonly IBlockchainLogProcessingService _blockchainLogProcessing;
        protected readonly IEthApiContractService _ethApiContractService;

        public TokenTransferLogProcessingService(IBlockchainLogProcessingService blockchainLogProcessing,
            IEthApiContractService ethApiContractService)
        {
            _blockchainLogProcessing = blockchainLogProcessing;
            _ethApiContractService = ethApiContractService;
        }

        public static NewFilterInput CreateTransferFilterInput(string[] contractAddresses = null)
        {
            var topics = new List<object>
            {
                Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                Event<TransferSingleEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic(),
                Event<TransferBatchEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()
            };

            var filterInput = new NewFilterInput
            {
                Topics = new object[] { topics.ToArray() }
            };

            if (contractAddresses != null && contractAddresses.Length > 0)
            {
                filterInput.Address = contractAddresses;
            }

            return filterInput;
        }

        public BlockchainProcessor CreateProcessorForTransactionLogStorage(
            ITransactionLogRepository repository,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight,
            uint minimumNumberOfConfirmations = 0,
            int reorgBuffer = 0,
            ILogProcessingObserver observer = null,
            string[] contractAddresses = null)
        {
            var filterInput = CreateTransferFilterInput(contractAddresses);
            var eventLogProcessingService = new EventLogProcessingService(_blockchainLogProcessing);
            return eventLogProcessingService.CreateProcessor(
                repository,
                filterInput,
                blockProgressRepository,
                log,
                numberOfBlocksPerRequest,
                retryWeight,
                minimumNumberOfConfirmations,
                reorgBuffer,
                observer);
        }

        public BlockchainProcessor CreateProcessor(
            ITokenTransferLogRepository repository,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight,
            uint minimumNumberOfConfirmations = 0,
            int reorgBuffer = 0,
            ILogProcessingObserver observer = null,
            string[] contractAddresses = null)
        {
            var filterInput = CreateTransferFilterInput(contractAddresses);

            var logProcessorHandler = new ProcessorHandler<FilterLog>(
                action: async (filterLog) =>
                    await ProcessTransferLogAsync(repository, filterLog).ConfigureAwait(false),
                criteria: (filterLog) => filterLog.Removed == false);

            return _blockchainLogProcessing.CreateProcessor(
                new ProcessorHandler<FilterLog>[] { logProcessorHandler },
                minimumNumberOfConfirmations,
                reorgBuffer,
                filterInput,
                blockProgressRepository,
                log,
                numberOfBlocksPerRequest,
                retryWeight,
                observer);
        }

        public async Task ProcessAllTransferLogsAsync(
            ITokenTransferLogRepository repository,
            BigInteger? fromBlockNumber,
            BigInteger? toBlockNumber,
            CancellationToken cancellationToken,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight,
            string[] contractAddresses = null)
        {
            var filterInput = CreateTransferFilterInput(contractAddresses);

            var logs = await _blockchainLogProcessing.GetAllEvents(
                filterInput, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

            foreach (var log in logs)
            {
                if (log.Removed) continue;
                await ProcessTransferLogAsync(repository, log).ConfigureAwait(false);
            }
        }

        public static async Task ProcessTransferLogAsync(ITokenTransferLogRepository repository, FilterLog log)
        {
            var transferLogs = DecodeTransferLog(log);
            foreach (var transferLog in transferLogs)
            {
                await repository.UpsertAsync(transferLog).ConfigureAwait(false);
            }
        }

        public static List<TokenTransferLog> DecodeTransferLog(FilterLog log)
        {
            var results = new List<TokenTransferLog>();

            var transferSig = Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();
            var transferSingleSig = Event<TransferSingleEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();
            var transferBatchSig = Event<TransferBatchEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();

            if (log.Topics == null || log.Topics.Length == 0) return results;

            var eventSig = log.Topics[0]?.ToString();

            if (eventSig == transferSig)
            {
                if (log.Topics.Length == 4)
                {
                    var decoded = log.DecodeEvent<ERC721Transfer>();
                    if (decoded?.Event != null)
                    {
                        results.Add(CreateTransferLog(log, decoded.Event.From, decoded.Event.To,
                            null, decoded.Event.TokenId.ToString(), null, "ERC721"));
                    }
                }
                else
                {
                    var decoded = log.DecodeEvent<ERC20Transfer>();
                    if (decoded?.Event != null)
                    {
                        results.Add(CreateTransferLog(log, decoded.Event.From, decoded.Event.To,
                            decoded.Event.Value.ToString(), null, null, "ERC20"));
                    }
                }
            }
            else if (eventSig == transferSingleSig)
            {
                var decoded = log.DecodeEvent<TransferSingleEventDTO>();
                if (decoded?.Event != null)
                {
                    results.Add(CreateTransferLog(log, decoded.Event.From, decoded.Event.To,
                        decoded.Event.Value.ToString(), decoded.Event.Id.ToString(),
                        decoded.Event.Operator, "ERC1155"));
                }
            }
            else if (eventSig == transferBatchSig)
            {
                var decoded = log.DecodeEvent<TransferBatchEventDTO>();
                if (decoded?.Event != null && decoded.Event.Ids != null)
                {
                    for (int i = 0; i < decoded.Event.Ids.Count; i++)
                    {
                        var value = decoded.Event.Values != null && i < decoded.Event.Values.Count
                            ? decoded.Event.Values[i].ToString()
                            : "0";

                        results.Add(CreateTransferLog(log, decoded.Event.From, decoded.Event.To,
                            value, decoded.Event.Ids[i].ToString(),
                            decoded.Event.Operator, "ERC1155"));
                    }
                }
            }

            return results;
        }

        private static TokenTransferLog CreateTransferLog(
            FilterLog log,
            string from, string to,
            string amount, string tokenId,
            string operatorAddress, string tokenType)
        {
            return new TokenTransferLog
            {
                TransactionHash = log.TransactionHash,
                LogIndex = (long)(log.LogIndex?.Value ?? 0),
                BlockNumber = (long)(log.BlockNumber?.Value ?? 0),
                BlockHash = log.BlockHash,
                ContractAddress = log.Address,
                EventHash = log.Topics?[0]?.ToString(),
                FromAddress = from,
                ToAddress = to,
                Amount = amount,
                TokenId = tokenId,
                OperatorAddress = operatorAddress,
                TokenType = tokenType,
                IsCanonical = true
            };
        }
    }
}
