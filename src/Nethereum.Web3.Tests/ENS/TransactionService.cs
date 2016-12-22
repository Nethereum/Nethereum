using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3.Tests
{
    public class TransactionService
    {
        private readonly Web3 _web3;
        private readonly int _retryMiliseconds;

        public TransactionService(Web3 web3, int retryMiliseconds = 1000)
        {
            _web3 = web3;
            _retryMiliseconds = retryMiliseconds;
        }

        public async Task<TransactionReceipt> SendRequestAsync(Func<Task<string>> transactionFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transaction = await transactionFunction();
            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction);
            while (receipt == null)
            {
                Thread.Sleep(_retryMiliseconds);
                tokenSource?.Token.ThrowIfCancellationRequested();
                receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction);
            }
            return receipt;
        }

        public async Task<TransactionReceipt> DeployContractAsync(Func<Task<string>> deployFunction,
            CancellationTokenSource tokenSource = null)
        {
            var transactionReceipt = await SendRequestAsync(deployFunction, tokenSource);
            var contractAddress = transactionReceipt.ContractAddress;
            var code = await _web3.Eth.GetCode.SendRequestAsync(contractAddress);
            if(code == "0x") throw new ContractDeploymentException("Code not deployed succesfully", transactionReceipt);
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