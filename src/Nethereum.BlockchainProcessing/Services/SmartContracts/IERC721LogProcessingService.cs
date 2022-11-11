using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public interface IERC721LogProcessingService
    {
        Task<List<ERC721TokenOwnerInfo>> GetErc721OwnedByAccountUsingAllTransfersForContracts(
            string[] contractAddresses, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<ERC721TokenOwnerInfo>> GetErc721OwnedByAccountUsingAllTransfersForContract(
            string contractAddress, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<ERC721TokenOwnerInfo>> GetAllCurrentOwnersProcessingAllTransferEvents(string contractAddress,
            CancellationToken cancellationToken);

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContract(string contractAddress,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContract(string contractAddress,
            CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsForContracts(string[] contractAddresses,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsFromAndToAccount(string[] contractAddresses, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsFromAndToAccount(string contractAddress, string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);
    }
}