using System.Threading;
using System.Threading.Tasks;
using Nethereum.Geth;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class GethTester
    {
        public Web3 Web3 { get; set; }
        public string Account { get; set; }
        public string Password { get; set; }

        public GethTester(Web3 web3, string account, string password)
        {
           this.Web3 = new Web3(new ManagedAccount(account, password), web3.Client);
            this.Account = account;
            this.Password = password;
        }

        public async Task<bool> UnlockAccount()
        {
            return await Web3.Personal.UnlockAccount.SendRequestAsync(Account, Password, 600);
        }

        public async Task<bool> StartMining()
        {
            return await new Web3Geth(Web3.Client).Miner.Start.SendRequestAsync();
        }

        public async Task<bool> StopMining()
        {
            return await new Web3Geth(Web3.Client).Miner.Stop.SendRequestAsync();
        }

        public async Task<bool> LockAccount()
        {
            return await Web3.Personal.LockAccount.SendRequestAsync(Account);
        }

        public async Task<TransactionReceipt> DeployTestContractLocal(string contractByteCode)
        {

            var transactionHash = await Web3.Eth.DeployContract.SendRequestAsync(contractByteCode, Account, new HexBigInteger(900000));
            Assert.NotNull(transactionHash);
            //the contract should be mining now

            var result = await StartMining();
            //Assert.True(result, "Mining should have started");
            //get the contract address 
            var receipt = await GetTransactionReceipt(transactionHash);

            Assert.NotNull(receipt.ContractAddress);

            result = await StopMining();
            Assert.True(result, "Mining should have stopped");
            return receipt;
        }


        public async Task<TransactionReceipt> DeployTestContractLocal(string abi, string contractByteCode, params object[] constructorParameters)
        {

            var transactionHash = await Web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, Account, new HexBigInteger(900000), constructorParameters);
            Assert.NotNull(transactionHash);
            //the contract should be mining now
            var result = await StartMining();
            //Assert.True(result, "Mining should have started");
            //get the contract address 
            var receipt = await GetTransactionReceipt(transactionHash);

            Assert.NotNull(receipt.ContractAddress);

            result = await StopMining();
            Assert.True(result, "Mining should have stopped");
            return receipt;
        }


        public async Task<TransactionReceipt> GetTransactionReceipt(string transactionHash)
        {
            TransactionReceipt receipt = null;

            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }
            return receipt;
        }
    }
}