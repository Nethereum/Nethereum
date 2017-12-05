using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class NonceTests
    {
        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnMultipleWeb3sMultithreaded()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var client = ClientFactory.GetClient();
            var nonceProvider = new InMemoryNonceService(senderAddress, client);
            //tested with 1000
            var listTasks = 10;
            var taskItems = new List<int>();
            for (int i = 0; i < listTasks; i++)
            {
                taskItems.Add(i);
            }

            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var concurrentDictionary = new ConcurrentDictionary<int, string>(concurrencyLevel, listTasks * 2);


            Parallel.ForEach(taskItems, async (item, state) =>
            {
                var account = new Account(privateKey);
                account.NonceService = nonceProvider;
                var web3 = new Web3(account, client);
                var txn = await
                    web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000),
                        null, multiplier);
                concurrentDictionary.TryAdd(item, txn);
            });

            var web31 = new Web3(new Account(privateKey), client);
            var pollService = new TransactionReceiptPollingService(web31.TransactionManager);

            for (int i = 0; i < listTasks; i++)
            {
                string txn = null;
                concurrentDictionary.TryGetValue(i, out txn);
                var receipt = pollService.PollForReceiptAsync(txn);
                Assert.NotNull(receipt);
            }
        }


        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnMultipleWeb3sSingleThreaded()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var client = ClientFactory.GetClient();
            var nonceProvider = new InMemoryNonceService(senderAddress, client);
            var account = new Account(privateKey) {NonceService = nonceProvider};
            var web31 = new Web3(account, client);
            
            var txn1 = await
                web31.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var web32 = new Web3(account, client);
            

            var txn2 = await
                web32.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var web33 = new Web3(account, client);
            
            var txn3 = await
                web33.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var pollService = new TransactionReceiptPollingService(web31.TransactionManager);

            var receipt1 = pollService.PollForReceiptAsync(txn1);
            var receipt2 = pollService.PollForReceiptAsync(txn2);
            var receipt3 = pollService.PollForReceiptAsync(txn3);

            Assert.NotNull(receipt1);
            Assert.NotNull(receipt2);
            Assert.NotNull(receipt3);
        }


        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnSingleWeb3SingleThreaded()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = new Web3(new Account(privateKey), ClientFactory.GetClient());

            var txn1 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var txn2 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var txn3 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null, multiplier);

            var pollService = new TransactionReceiptPollingService(web3.TransactionManager);

            var receipt1 = pollService.PollForReceiptAsync(txn1);
            var receipt2 = pollService.PollForReceiptAsync(txn2);
            var receipt3 = pollService.PollForReceiptAsync(txn3);

            Assert.NotNull(receipt1);
            Assert.NotNull(receipt2);
            Assert.NotNull(receipt3);
        }
    }
}