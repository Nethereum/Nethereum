using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3.TransactionReceipts
{
    public class TransactionReceiptPollingService : ITransactionReceiptService
    {
        private readonly Nethereum.Web3.Web3 _web3;
        private readonly int _retryMiliseconds;

        public TransactionReceiptPollingService(Nethereum.Web3.Web3 web3, int retryMiliseconds = 100)
        {
            _web3 = web3;
            _retryMiliseconds = retryMiliseconds;
        }

        public Task<TransactionReceipt> SendRequestAsync(TransactionInput transactionInput,
            CancellationTokenSource tokenSource = null)
        {
            return SendRequestAsync(() => _web3.TransactionManager.SendTransactionAsync(transactionInput), tokenSource);
        }

        public Task<List<TransactionReceipt>> SendRequestAsync(IEnumerable<TransactionInput> transactionInputs,
            CancellationTokenSource tokenSource = null)
        {
            var funcs = new List<Func<Task<string>>>();
            foreach (var transactionInput in transactionInputs)
            {
                funcs.Add(() => _web3.TransactionManager.SendTransactionAsync(transactionInput));
            }
            return SendRequestAsync(funcs.ToArray(), tokenSource);
        }

        public async Task<TransactionReceipt> SendRequestAsync(Func<Task<string>> transactionFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transaction = await transactionFunction();
            return await PollForReceiptAsync(tokenSource, transaction);
        }

        private async Task<TransactionReceipt> PollForReceiptAsync(CancellationTokenSource tokenSource, string transaction)
        {
            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            while (receipt == null)
            {
                await Task.Delay(_retryMiliseconds);
                tokenSource?.Token.ThrowIfCancellationRequested();
                receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
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
                var receipt =  await PollForReceiptAsync(tokenSource, transaction);
                receipts.Add(receipt);
            }
            return receipts;
        }

        public async Task<TransactionReceipt> DeployContractAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await SendRequestAsync(deployFunction, tokenSource);
            var contractAddress = transactionReceipt.ContractAddress;
            var code = await _web3.Eth.GetCode.SendRequestAsync(contractAddress).ConfigureAwait(false);
            if (code == "0x") throw new ContractDeploymentException("Code not deployed succesfully", transactionReceipt);
            return transactionReceipt;
        }

        public async Task<string> DeployContractAndGetAddressAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await DeployContractAsync(deployFunction, tokenSource);
            return transactionReceipt.ContractAddress;
        }

    }
}
