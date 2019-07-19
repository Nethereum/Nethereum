using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class FilterLogStorageStepHandler : ProcessorBaseHandler<FilterLogVO>
    {
        private readonly ITransactionLogRepository _transactionLogRepository;

        public FilterLogStorageStepHandler(ITransactionLogRepository transactionLogRepository)
        {
            _transactionLogRepository = transactionLogRepository;
        }

        protected override Task ExecuteInternalAsync(FilterLogVO filterLog)
        {
            return _transactionLogRepository.UpsertAsync(filterLog);
        }
    }
}
