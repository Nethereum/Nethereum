using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Maker.ERC20Token;
using Nethereum.Maker.ERC20Token.Events.DTO;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTests
{
    [TestClass]
    public class MakerTest
    {
        [TestMethod]
        public void IpcTest()
        {
            var mkrExample = new MakerTokenRegistryServiceExample();
            var testResult = mkrExample.RunExampleAsync(new IpcClient("geth.ipc")).Result;
        }
    }
    public class MakerTokenRegistryServiceExample
    {
        public async Task<bool> RunExampleAsync(IClient client = null)
        {
            var web3 = new Web3();
            if (client != null)
            {
                web3 = new Web3(client);
            }

            var makerRegistry = new MakerTokenRegistryService(web3,
                "0x877c5369c747d24d9023c88c1aed1724f1993efe");

            var mkrTokenService = await makerRegistry.GetEthTokenServiceAsync("MKR");

            var totalSupply = await mkrTokenService.GetTotalSupplyAsync<BigInteger>();
            Debug.WriteLine("Maker Supply");
            Debug.WriteLine(totalSupply);

            var address = "0x63c2ee74201b99de5e76198a7b2e6540bca83347";



            var balance = await mkrTokenService.GetBalanceOfAsync<BigInteger>(address);
            Debug.WriteLine("Balance " + address);
            Debug.WriteLine(balance);


            var ethTokenService = await makerRegistry.GetEthTokenServiceAsync(MakerTokenRegistryService.MakerTokenSymbols.ETH);

            var ethTotalSupply = await ethTokenService.GetTotalSupplyAsync<BigInteger>();
            Debug.WriteLine("Eth Supply");
            Debug.WriteLine(ethTotalSupply);


            var result = await web3.Personal.UnlockAccount.SendRequestAsync(address, "password", new HexBigInteger(600));

            var newAddress = await web3.Personal.NewAccount.SendRequestAsync("password");
            Debug.WriteLine("New address");
            Debug.WriteLine(newAddress);


            var transactionHash = await mkrTokenService.TransferAsync(address, newAddress, 10, new HexBigInteger(150000));
            Debug.WriteLine("Transfering 10 MKR to " + newAddress);
            Debug.WriteLine("Transfer txId:");
            Debug.WriteLine(transactionHash);
            //wait to be mined
            var transferReceipt = await GetTransactionReceiptAsync(web3.Eth.Transactions, transactionHash);

            var filterId = await mkrTokenService.GetTransferEvent().CreateFilterAsync(new BlockParameter(500000)); //<object, string>(null, "0xbb7e97e5670d7475437943a1b314e661d7a9fa2a", new BlockParameter(1000));


            var transfers = await mkrTokenService.GetTransferEvent().GetAllChanges<Transfer>(filterId);


            var balanceNewAddress = await mkrTokenService.GetBalanceOfAsync<BigInteger>(newAddress);
            Debug.WriteLine("Balance of " + newAddress);
            Debug.WriteLine(balanceNewAddress);

            balance = await mkrTokenService.GetBalanceOfAsync<BigInteger>(address);
            Debug.WriteLine("Balance " + address);
            Debug.WriteLine(balance);

            Debug.WriteLine("Total Transfers since block 500000");
            Debug.WriteLine(transfers.Count);

            foreach (var transfer in transfers)
            {
                Debug.WriteLine("From:");
                Debug.WriteLine(transfer.Event.AddressFrom);
                Debug.WriteLine("To:");
                Debug.WriteLine(transfer.Event.AddressTo);
                Debug.WriteLine("Amount:");
                Debug.WriteLine(transfer.Event.Value);

            }

            return true;
        }

        private static async Task<TransactionReceipt> GetTransactionReceiptAsync(EthTransactionsService transactionService, string transactionHash)
        {
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(5000);
                receipt = await transactionService.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }
            return receipt;
        }
    }

}