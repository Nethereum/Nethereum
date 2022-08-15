using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public class ERC20LogProcessingService : IERC20LogProcessingService
    {
        private readonly IBlockchainLogProcessingService _blockchainLogProcessing;
        private readonly IEthApiContractService _ethApiContractService;

        public ERC20LogProcessingService(IBlockchainLogProcessingService blockchainLogProcessing,
            IEthApiContractService ethApiContractService)
        {
            _blockchainLogProcessing = blockchainLogProcessing;
            _ethApiContractService = ethApiContractService;
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