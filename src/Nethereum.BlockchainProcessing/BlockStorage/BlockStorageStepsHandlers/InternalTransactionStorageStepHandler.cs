using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class InternalTransactionStorageStepHandler : ProcessorBaseHandler<TransactionReceiptVO>
    {
        private readonly IInternalTransactionRepository _repository;
        private readonly Func<string, Task<List<InternalTransaction>>> _traceProvider;

        public InternalTransactionStorageStepHandler(
            IInternalTransactionRepository repository,
            Func<string, Task<List<InternalTransaction>>> traceProvider)
        {
            _repository = repository;
            _traceProvider = traceProvider;
        }

        protected override async Task ExecuteInternalAsync(TransactionReceiptVO receipt)
        {
            var input = receipt.Transaction?.Input;
            var isContractCall = !string.IsNullOrEmpty(input) && input != "0x";
            var isContractCreation = receipt.IsForContractCreation();

            if (!isContractCall && !isContractCreation) return;

            var txHash = receipt.Transaction.TransactionHash;
            var internalTxs = await _traceProvider(txHash).ConfigureAwait(false);
            if (internalTxs == null) return;

            foreach (var itx in internalTxs)
            {
                itx.BlockNumber = (long)(receipt.BlockNumber?.Value ?? 0);
                itx.BlockHash = receipt.Transaction?.BlockHash;
                itx.TransactionHash = txHash;
                itx.IsCanonical = true;
                itx.UpdateRowDates();
                await _repository.UpsertAsync(itx).ConfigureAwait(false);
            }
        }
    }
}
