using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public static class TokenDenormalizerProcessingService
    {
        private static readonly string ERC20TransferEventHash =
            Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();

        private static readonly string TransferSingleEventHash =
            Event<TransferSingleEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();

        private static readonly string TransferBatchEventHash =
            Event<TransferBatchEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic()?.ToString();

        public static string[] GetTransferEventHashes()
        {
            return new[] { ERC20TransferEventHash, TransferSingleEventHash, TransferBatchEventHash };
        }

        public static bool IsTransferEvent(string eventHash)
        {
            if (string.IsNullOrEmpty(eventHash)) return false;
            return eventHash == ERC20TransferEventHash
                || eventHash == TransferSingleEventHash
                || eventHash == TransferBatchEventHash;
        }

        public static async Task<int> ProcessBatchAsync(
            IEnumerable<ITransactionLogView> rawLogs,
            ITokenTransferLogRepository repository)
        {
            int count = 0;
            foreach (var rawLog in rawLogs)
            {
                if (!IsTransferEvent(rawLog.EventHash)) continue;

                var filterLog = rawLog.ToFilterLog();
                var transferLogs = TokenTransferLogProcessingService.DecodeTransferLog(filterLog);
                foreach (var transferLog in transferLogs)
                {
                    await repository.UpsertAsync(transferLog).ConfigureAwait(false);
                    count++;
                }
            }
            return count;
        }
    }
}
