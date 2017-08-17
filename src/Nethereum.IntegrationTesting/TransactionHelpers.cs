using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.IntegrationTesting
{
    public class TransactionHelpers
    {
        public async Task<string> DeployContract(Web3.Web3 web3, string addressFrom, string pass, string bytecode)
        {
            var  receipt = await SendAndMineTransactionAsync(web3, addressFrom, pass, () => web3.Eth.DeployContract.SendRequestAsync(bytecode, addressFrom, new HexBigInteger(900000)));
            return receipt.ContractAddress;
        }

        public async Task<string> DeployContract(Web3.Web3 web3, string addressFrom, string pass, string bytecode, bool mineIt)
        {
            if (mineIt) return await DeployContract(web3, addressFrom, pass, bytecode);
            var receipt = await SendTransactionAsync(web3, addressFrom, pass, () => web3.Eth.DeployContract.SendRequestAsync(bytecode, addressFrom, new HexBigInteger(900000)));
            return receipt.ContractAddress;
        }

        public async Task<TransactionReceipt> SendTransactionAsync(Web3.Web3 web3, string addressFrom,
            string pass, Func<Task<string>> transactionTask)
        {
            var receitps = await SendTransactionsAsync(web3, addressFrom, pass, transactionTask);
            return receitps[0];
        }

        public async Task<TransactionReceipt[]> SendTransactionsAsync(Web3.Web3 web3, string addressFrom,
            string pass, params Func<Task<string>>[] transactionsTask)
        {
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(addressFrom, pass, new HexBigInteger(600000));
            if (result != true) throw new Exception("Acccount not unlocked");

            var txIds = new List<string>();

            foreach (var transactionTask in transactionsTask)
            {
                var txId = await transactionTask();
                txIds.Add(txId);
            }

            var receipts = new List<TransactionReceipt>();

            foreach (var txId in txIds)
            {
                receipts.Add(await TransactionReceipt(web3, txId));
            }

            return receipts.ToArray();
        }

        public async Task<TransactionReceipt> SendAndMineTransactionAsync(Web3.Web3 web3, string addressFrom,
            string pass, Func<Task<string>> transactionTask)
        {
            var receitps =  await SendAndMineTransactionsAsync(web3, addressFrom, pass, transactionTask);
            return receitps[0];
        }

        public async Task<TransactionReceipt[]> SendAndMineTransactionsAsync(Web3.Web3 web3, string addressFrom,
            string pass, params Func<Task<string>>[] transactionsTask)
        {
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(addressFrom, pass, new HexBigInteger(600000));
            if (result != true) throw new Exception("Acccount not unlocked");

            result = await web3.Miner.Start.SendRequestAsync(6);

            if (result != true) throw new Exception("Could not start mining");

            var txIds = new List<string>();

            foreach (var transactionTask in transactionsTask)
            {
               var txId = await transactionTask();
                txIds.Add(txId);
            }

            var receipts = new List<TransactionReceipt>();

            foreach (var txId in txIds)
            {
                receipts.Add(await TransactionReceipt(web3, txId));
            }

            result = await web3.Miner.Stop.SendRequestAsync();

            if (result != true) throw new Exception("Could not stop mining");
            return receipts.ToArray();
        }

        private static async Task<TransactionReceipt> TransactionReceipt(Web3.Web3 web3, string transaction)
        {
            
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction);

            while (receipt == null)
            {
                Thread.Sleep(5000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction);
            }
            return receipt;
        }
    }
}