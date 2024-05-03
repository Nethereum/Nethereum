using Nethereum.Mud.Contracts.StoreEvents;
using System.Numerics;
using Nethereum.BlockchainProcessing.Services;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class ResourceAccessLogProcessingExtensions
    {
        public static Task<IEnumerable<ResourceAccessTableRecord>> GetResourceAccessTableRecordsFromLogsAsync(this StoreEventsLogProcessingService storeEventsLogProcessing,
            string contractAddress, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                       int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return storeEventsLogProcessing.GetTableRecordsFromLogsAsync<ResourceAccessTableRecord>(contractAddress, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }
    }
}
