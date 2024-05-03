using Nethereum.BlockchainProcessing.Services;
using Nethereum.Mud.Contracts.StoreEvents;
using System.Numerics;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class FunctionSelectorsLogProcessingExtensions
    {
        public static Task<IEnumerable<FunctionSelectorsTableRecord>> GetFunctionSelectorsTableRecordsFromLogsAsync(this StoreEventsLogProcessingService storeEventsLogProcessing,
            string contractAddress, BigInteger? fromBlockNumber, BigInteger? toBlockNumber, CancellationToken cancellationToken, int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
                       int retryWeight = BlockchainLogProcessingService.RetryWeight)
        {
            return storeEventsLogProcessing.GetTableRecordsFromLogsAsync<FunctionSelectorsTableRecord>(contractAddress, fromBlockNumber, toBlockNumber, cancellationToken, numberOfBlocksPerRequest, retryWeight);
        }
    }

}

