using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.Storage.Repositories;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.StorageStepsHandlers
{
    public class FilterLogStepStorageHandler : ProcessorBaseHandler<FilterLogVO>
    {
        private readonly ITransactionLogRepository _transactionLogRepository;

        public FilterLogStepStorageHandler(ITransactionLogRepository transactionLogRepository)
        {
            _transactionLogRepository = transactionLogRepository;
        }

        protected override Task ExecuteInternalAsync(FilterLogVO filterLog)
        {
            return _transactionLogRepository.UpsertAsync(filterLog);
        }
    }
}
