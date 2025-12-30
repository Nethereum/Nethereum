using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public interface IERC20LogProcessingService
    {
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

        Task<List<EventLog<TransferEventDTO>>> GetAllTransferEventsFromAndToAccount(string account,
            BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight);
    }
}