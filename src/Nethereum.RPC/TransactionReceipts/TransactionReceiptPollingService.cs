using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Exceptions;

namespace Nethereum.RPC.TransactionReceipts
{

#if !DOTNET35
    public class TransactionReceiptServiceFactory
    {
        public static ITransactionReceiptService GetDefaultransactionReceiptService(ITransactionManager transactionManager)
        {
            return new TransactionReceiptPollingService(transactionManager);
        }
    }

    public class TransactionReceiptPollingService : ITransactionReceiptService
    {
        private readonly ITransactionManager _transactionManager;

        private int _retryMilliseconds;
        private readonly object _lockingObject = new object();
        public int GetPollingRetryIntervalInMilliseconds()
        {
            lock (_lockingObject)
            {
                return _retryMilliseconds;
            }
        }

        public void SetPollingRetryIntervalInMilliseconds(int retryMilliseconds)
        {
            lock (_lockingObject)
            {
                _retryMilliseconds = retryMilliseconds;
            }
        }

        public TransactionReceiptPollingService(ITransactionManager transactionManager, int retryMilliseconds = 100)
        {
            _transactionManager = transactionManager;
            _retryMilliseconds = retryMilliseconds;
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TransactionInput transactionInput,
           CancellationToken cancellationToken = default)
        {
            return SendRequestAndWaitForReceiptAsync(() => _transactionManager.SendTransactionAsync(transactionInput), cancellationToken);
        }

        public Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<TransactionInput> transactionInputs,
           CancellationToken cancellationToken = default)
        {
            var funcs = new List<Func<Task<string>>>();
            foreach (var transactionInput in transactionInputs)
            {
                funcs.Add(() => _transactionManager.SendTransactionAsync(transactionInput));
            }
            return SendRequestsAndWaitForReceiptAsync(funcs.ToArray(), cancellationToken);
        }

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(Func<Task<string>> transactionFunction,
           CancellationToken cancellationToken = default)
        {
            var transaction = await transactionFunction().ConfigureAwait(false);
            return await PollForReceiptAsync(transaction, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> PollForReceiptAsync(string transaction, CancellationToken cancellationToken = default)
        {
            var getTransactionReceipt = new EthGetTransactionReceipt(_transactionManager.Client);
            var receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            while (receipt == null)
            {
                if (cancellationToken !=  CancellationToken.None)
                {
                    await Task.Delay(GetPollingRetryIntervalInMilliseconds(), cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else
                {
                    await Task.Delay(GetPollingRetryIntervalInMilliseconds()).ConfigureAwait(false);
                    
                }

                receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            }
            return receipt;
        }


        public async Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<Func<Task<string>>> transactionFunctions,
            CancellationToken cancellationToken = default)
        {
            var txnList = new List<string>();
            foreach (var transactionFunction in transactionFunctions)
            {
                txnList.Add(await transactionFunction().ConfigureAwait(false));
            }

            var receipts = new List<TransactionReceipt>();
            foreach (var transaction in txnList)
            {
                var receipt = await PollForReceiptAsync(transaction, cancellationToken).ConfigureAwait(false);
                receipts.Add(receipt);
            }
            return receipts;
        }

        public async Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Func<Task<string>> deployFunction,
           CancellationToken cancellationToken = default)
        {
            var transactionReceipt = await SendRequestAndWaitForReceiptAsync(deployFunction, cancellationToken).ConfigureAwait(false);
            return await ValidateDeploymentTransactionReceipt(transactionReceipt).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> ValidateDeploymentTransactionReceipt(TransactionReceipt transactionReceipt)
        {
            if (transactionReceipt.Status.Value != 1)
            {
                var contractAddress = transactionReceipt.ContractAddress;
                var ethGetCode = new EthGetCode(_transactionManager.Client);
                var code = await ethGetCode.SendRequestAsync(contractAddress).ConfigureAwait(false);
                if (code == "0x")
                    throw new ContractDeploymentException("Contract code not deployed successfully", transactionReceipt);
            }

            return transactionReceipt;
        }

        public async Task<string> DeployContractAndGetAddressAsync(Func<Task<string>> deployFunction,
           CancellationToken cancellationToken = default)
        {
            var transactionReceipt = await DeployContractAndWaitForReceiptAsync(deployFunction, cancellationToken).ConfigureAwait(false);
            return transactionReceipt.ContractAddress;
        }

        public Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationToken cancellationToken = default)
        {
             return DeployContractAndWaitForReceiptAsync(() => _transactionManager.SendTransactionAsync(transactionInput), cancellationToken);
        }
    }
#endif
}
