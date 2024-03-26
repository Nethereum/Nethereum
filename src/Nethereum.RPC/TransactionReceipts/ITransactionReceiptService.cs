using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.TransactionReceipts
{
    public interface ITransactionReceiptService
    {
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(Func<Task<string>> transactionFunction,
            CancellationToken cancellationToken = default);

        Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Func<Task<string>> deployFunction,
             CancellationToken cancellationToken = default);

        Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(TransactionInput transactionInput,
            CancellationToken cancellationToken = default);

        Task<string> DeployContractAndGetAddressAsync(Func<Task<string>> deployFunction,
            CancellationToken cancellationToken = default);

        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TransactionInput transactionInput,
            CancellationToken cancellationToken = default);

        Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<TransactionInput> transactionInputs,
            CancellationToken cancellationToken = default);

        Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<Func<Task<string>>> transactionFunctions,
          CancellationToken cancellationToken = default);
        Task<TransactionReceipt> PollForReceiptAsync(string transaction, CancellationToken cancellationToken = default);

        int GetPollingRetryIntervalInMilliseconds();
        void SetPollingRetryIntervalInMilliseconds(int retryMilliseconds);
        Task<TransactionReceipt> ValidateDeploymentTransactionReceipt(TransactionReceipt transactionReceipt);
    }
}