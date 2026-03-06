using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class FilterLogStorageStepHandler : ProcessorBaseHandler<FilterLogVO>
    {
        private readonly ITransactionLogRepository _transactionLogRepository;
        private readonly LogStorageOptions _logStorageOptions;

        public FilterLogStorageStepHandler(ITransactionLogRepository transactionLogRepository)
        {
            _transactionLogRepository = transactionLogRepository;
        }

        public FilterLogStorageStepHandler(ITransactionLogRepository transactionLogRepository,
            LogStorageOptions logStorageOptions) : this(transactionLogRepository)
        {
            _logStorageOptions = logStorageOptions;
        }

        protected override Task ExecuteInternalAsync(FilterLogVO filterLog)
        {
            if (_logStorageOptions != null)
            {
                var eventSignature = filterLog.Log.EventSignature();
                var contractAddress = filterLog.Log.Address;
                if (!_logStorageOptions.ShouldStoreLog(eventSignature, contractAddress))
                {
#if !NETSTANDARD1_1 && !NET451
                    return Task.CompletedTask;
#else
                    return Task.FromResult(0);
#endif
                }
            }

            return _transactionLogRepository.UpsertAsync(filterLog);
        }
    }
}
