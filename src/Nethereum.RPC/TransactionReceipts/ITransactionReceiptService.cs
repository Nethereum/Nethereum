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
            CancellationToken token = default(CancellationToken));

        Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Func<Task<string>> deployFunction,
            CancellationToken token = default(CancellationToken));

        Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(TransactionInput transactionInput,
            CancellationToken token = default(CancellationToken));

        Task<string> DeployContractAndGetAddressAsync(Func<Task<string>> deployFunction,
            CancellationToken token = default(CancellationToken));

        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TransactionInput transactionInput,
            CancellationToken token = default(CancellationToken));

        Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<TransactionInput> transactionInputs,
            CancellationToken token = default(CancellationToken));

        Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<Func<Task<string>>> transactionFunctions,
          CancellationToken token = default(CancellationToken));

        Task<TransactionReceipt> PollForReceiptAsync(string transaction, CancellationToken token = default(CancellationToken));
    }
}