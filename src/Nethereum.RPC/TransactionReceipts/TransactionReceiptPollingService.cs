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
        private readonly int _retryMiliseconds;

        public TransactionReceiptPollingService(ITransactionManager transactionManager, int retryMiliseconds = 100)
        {
            _transactionManager = transactionManager;
            _retryMiliseconds = retryMiliseconds;
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TransactionInput transactionInput,
            CancellationTokenSource tokenSource = null)
        {
            return SendRequestAndWaitForReceiptAsync(() => _transactionManager.SendTransactionAsync(transactionInput), tokenSource);
        }

        public Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<TransactionInput> transactionInputs,
            CancellationTokenSource tokenSource = null)
        {
            var funcs = new List<Func<Task<string>>>();
            foreach (var transactionInput in transactionInputs)
            {
                funcs.Add(() => _transactionManager.SendTransactionAsync(transactionInput));
            }
            return SendRequestsAndWaitForReceiptAsync(funcs.ToArray(), tokenSource);
        }

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(Func<Task<string>> transactionFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transaction = await transactionFunction().ConfigureAwait(false);
            return await PollForReceiptAsync(transaction, tokenSource).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> PollForReceiptAsync(string transaction, CancellationTokenSource tokenSource = null)
        {
            var getTransactionReceipt = new EthGetTransactionReceipt(_transactionManager.Client);
            var receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            while (receipt == null)
            {
                if (tokenSource != null)
                {
                    await Task.Delay(_retryMiliseconds, tokenSource.Token).ConfigureAwait(false);
                    tokenSource?.Token.ThrowIfCancellationRequested();
                }

                receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            }
            return receipt;
        }

        public async Task<List<TransactionReceipt>> SendRequestsAndWaitForReceiptAsync(IEnumerable<Func<Task<string>>> transactionFunctions,
            CancellationTokenSource tokenSource = null)
        {
            var txnList = new List<string>();
            foreach (var transactionFunction in transactionFunctions)
            {
                txnList.Add(await transactionFunction().ConfigureAwait(false));
            }

            var receipts = new List<TransactionReceipt>();
            foreach (var transaction in txnList)
            {
                var receipt = await PollForReceiptAsync(transaction, tokenSource).ConfigureAwait(false);
                receipts.Add(receipt);
            }
            return receipts;
        }

        public async Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await SendRequestAndWaitForReceiptAsync(deployFunction, tokenSource).ConfigureAwait(false);
            if (transactionReceipt.Status.Value != 1 )
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
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await DeployContractAndWaitForReceiptAsync(deployFunction, tokenSource).ConfigureAwait(false);
            return transactionReceipt.ContractAddress;
        }

        public Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationTokenSource tokenSource = null)
        {
             return DeployContractAndWaitForReceiptAsync(() => _transactionManager.SendTransactionAsync(transactionInput), tokenSource);
        }
    }
#endif
}
