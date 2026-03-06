using System.Collections.Generic;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public class EventLogProcessingService
    {
        protected readonly IBlockchainLogProcessingService _blockchainLogProcessing;

        public EventLogProcessingService(IBlockchainLogProcessingService blockchainLogProcessing)
        {
            _blockchainLogProcessing = blockchainLogProcessing;
        }

        public BlockchainProcessor CreateProcessor(
            ITransactionLogRepository repository,
            NewFilterInput filterInput,
            IBlockProgressRepository blockProgressRepository = null,
            ILogger log = null,
            int numberOfBlocksPerRequest = BlockchainLogProcessingService.DefaultNumberOfBlocksPerRequest,
            int retryWeight = BlockchainLogProcessingService.RetryWeight,
            uint minimumNumberOfConfirmations = 0,
            int reorgBuffer = 0,
            ILogProcessingObserver observer = null)
        {
            var logProcessorHandler = new ProcessorHandler<FilterLog>(
                action: async (filterLog) =>
                {
                    var filterLogVO = new FilterLogVO(null, null, filterLog);
                    await repository.UpsertAsync(filterLogVO).ConfigureAwait(false);
                },
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
    }
}
