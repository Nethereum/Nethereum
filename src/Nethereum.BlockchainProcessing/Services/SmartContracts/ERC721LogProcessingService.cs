using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.Util;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public class ERC721LogProcessingService : IERC721LogProcessingService
    {
        private readonly IBlockchainLogProcessingService _blockchainLogProcessing;
        private readonly IEthApiContractService _ethApiContractService;

        public ERC721LogProcessingService(IBlockchainLogProcessingService blockchainLogProcessing,
            IEthApiContractService ethApiContractService)
        {
            _blockchainLogProcessing = blockchainLogProcessing;
            _ethApiContractService = ethApiContractService;
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetErc721OwnedByAccountUsingAllTransfersForContracts(
            string[] contractAddresses, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var eventsFromOwner = await GetAllTransferEventsFromAndToAccount(contractAddresses,
                account, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
            var accountOwned = GetCurrentOwnersFromTransferEvents(eventsFromOwner);
            return accountOwned.Where(x => AddressExtensions.IsTheSameAddress(x.Owner, account)).ToList();
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetErc721OwnedByAccountUsingAllTransfersForContract(
            string contractAddress, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var eventsFromOwner = await GetAllTransferEventsFromAndToAccount(contractAddress,
                account, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
            var accountOwned = GetCurrentOwnersFromTransferEvents(eventsFromOwner);
            return accountOwned.Where(x => x.Owner.IsTheSameAddress(account)).ToList();
        }

        
        public async Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var transferEvents = await GetAllTransferEventsForContract(contractAddress, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

            return GetCurrentOwnersFromTransferEvents(transferEvents);
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var transferEvents = await GetAllTransferEventsForContracts(contractAddresses, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

            return GetCurrentOwnersFromTransferEvents(transferEvents);
            ;
        }

        public static List<ERC721TokenOwnerInfo> GetCurrentOwnersFromTransferEvents(List<EventLog<TransferEventDTO>> transferEvents)
        {
            transferEvents.Sort<TransferEventDTO>();
            var currentOwners = new Dictionary<string, ERC721TokenOwnerInfo>();
            foreach (var transferEvent in transferEvents)
            {
                var tokenId = transferEvent.Log.Address + "-" + transferEvent.Event.TokenId;
                currentOwners[tokenId] = new ERC721TokenOwnerInfo() { ContractAddress = transferEvent.Log.Address, TokenId = transferEvent.Event.TokenId, Owner = transferEvent.Event.To };
            }

            return currentOwners.Values.ToList();
        }

        public Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string contractAddress,
            CancellationToken cancellationToken)
        {
            return GetAllCurrentOwnersProcessingAllTransferEvents(contractAddress, null, null, cancellationToken);
        }

        public Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContract(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<TransferEventDTO>(contractAddresses: new[] { contractAddress }, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContracts(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return _blockchainLogProcessing.GetAllEventsForContracts<TransferEventDTO>(contractAddresses, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }

        public Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContract(string contractAddress,
            CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return GetAllTransferEventsForContract(contractAddress, null, null, cancellationToken,
                numberOfBlocksPerRequest, retryWeight);
        }

        public async Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsFromAndToAccount(string[] contractAddresses, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            var filterInputTo = new FilterInputBuilder<TransferEventDTO>().AddTopic(x => x.To, account)
                .Build(contractAddresses);
            var allEvents = await _blockchainLogProcessing.GetAllEvents<TransferEventDTO>(filterInputTo, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);

            var filterInputFrom = new FilterInputBuilder<TransferEventDTO>().AddTopic(x => x.From, account)
                .Build(contractAddresses);
            var eventsFrom = await _blockchainLogProcessing.GetAllEvents<TransferEventDTO>(filterInputFrom, fromBlockNumber, toBlockNumber,
                cancellationToken, numberOfBlocksPerRequest, retryWeight).ConfigureAwait(false);
            allEvents.AddRange(eventsFrom);
            return allEvents;
        }

        public Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsFromAndToAccount(string contractAddress, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return GetAllTransferEventsFromAndToAccount(new string[] { contractAddress }, account, fromBlockNumber,
                toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);

        }

    }
}