using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.Storage.Repositories;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.StorageStepsHandlers
{
    public class TransactionReceiptStepStorageHandler : ProcessorBaseHandler<TransactionReceiptVO>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAddressTransactionRepository _addressTransactionRepository;

        public TransactionReceiptStepStorageHandler(ITransactionRepository transactionRepository, IAddressTransactionRepository addressTransactionRepository = null)
        {
            _transactionRepository = transactionRepository;
            _addressTransactionRepository = addressTransactionRepository;
        }

        protected override async Task ExecuteInternalAsync(TransactionReceiptVO transactionReceiptVO)
        {
            await _transactionRepository.UpsertAsync(transactionReceiptVO).ConfigureAwait(false);

            if(_addressTransactionRepository != null)
            {
                var newContractAddress = transactionReceiptVO.IsForContractCreation() ? transactionReceiptVO.TransactionReceipt.ContractAddress : string.Empty;
                foreach (var address in transactionReceiptVO.GetAllRelatedAddresses())
                {
                    await _addressTransactionRepository.UpsertAsync(transactionReceiptVO,
                                                                    address, 
                                                                    null, 
                                                                    newContractAddress).ConfigureAwait(false);
                }
            }
        }
    }
}
