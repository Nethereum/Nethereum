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

        public Task<TransactionReceipt> SendRequestAsync(TransactionInput transactionInput,
            CancellationTokenSource tokenSource = null)
        {
            return SendRequestAsync(() => _transactionManager.SendTransactionAsync(transactionInput), tokenSource);
        }

        public Task<List<TransactionReceipt>> SendRequestAsync(IEnumerable<TransactionInput> transactionInputs,
            CancellationTokenSource tokenSource = null)
        {
            var funcs = new List<Func<Task<string>>>();
            foreach (var transactionInput in transactionInputs)
            {
                funcs.Add(() => _transactionManager.SendTransactionAsync(transactionInput));
            }
            return SendRequestAsync(funcs.ToArray(), tokenSource);
        }

        public async Task<TransactionReceipt> SendRequestAsync(Func<Task<string>> transactionFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transaction = await transactionFunction();
            return await PollForReceiptAsync(transaction, tokenSource);
        }

        public async Task<TransactionReceipt> PollForReceiptAsync(string transaction, CancellationTokenSource tokenSource = null)
        {
            var getTransactionReceipt = new EthGetTransactionReceipt(_transactionManager.Client);
            var receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            while (receipt == null)
            {
                await Task.Delay(_retryMiliseconds);
                tokenSource?.Token.ThrowIfCancellationRequested();
                receipt = await getTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            }
            return receipt;
        }

        public async Task<List<TransactionReceipt>> SendRequestAsync(IEnumerable<Func<Task<string>>> transactionFunctions,
            CancellationTokenSource tokenSource = null)
        {
            var txnList = new List<string>();
            foreach (var transactionFunction in transactionFunctions)
            {
                txnList.Add(await transactionFunction());
            }

            var receipts = new List<TransactionReceipt>();
            foreach (var transaction in txnList)
            {
                var receipt = await PollForReceiptAsync(transaction, tokenSource);
                receipts.Add(receipt);
            }
            return receipts;
        }

        public async Task<TransactionReceipt> DeployContractAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await SendRequestAsync(deployFunction, tokenSource);
            var contractAddress = transactionReceipt.ContractAddress;
            var ethGetCode = new EthGetCode(_transactionManager.Client);
            var code = await ethGetCode.SendRequestAsync(contractAddress).ConfigureAwait(false);
            if (code == "0x") throw new ContractDeploymentException("Code not deployed succesfully", transactionReceipt);
            return transactionReceipt;
        }

        public async Task<string> DeployContractAndGetAddressAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await DeployContractAsync(deployFunction, tokenSource);
            return transactionReceipt.ContractAddress;
        }

        public Task<TransactionReceipt> DeployContractAsync(TransactionInput transactionInput, CancellationTokenSource tokenSource = null)
        {
             return DeployContractAsync(() => _transactionManager.SendTransactionAsync(transactionInput), tokenSource);
        }
    }
#endif
}
