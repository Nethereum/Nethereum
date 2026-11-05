using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Nethereum.JsonRpc.Client;
using System.Net.Http;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.Accounts.IntegrationTests
{


    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class NonceTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public NonceTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnMultipleWeb3sMultithreaded()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";
            JsonRpc.Client.RpcClient.ConnectionTimeout = TimeSpan.FromSeconds(30.0);
            var multiplier = 7;

            var client = _ethereumClientIntegrationFixture.GetClient();
            var nonceProvider = new InMemoryNonceService(senderAddress, client);
            //tested with 1000
            var listTasks = 10;
            var taskItems = new List<int>();
            for (var i = 0; i < listTasks; i++)
                taskItems.Add(i);

            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var concurrentDictionary = new ConcurrentDictionary<int, string>(concurrencyLevel, listTasks * 2);


            Parallel.ForEach(taskItems, (item, state) =>
            {
                var account = new Account(privateKey, EthereumClientIntegrationFixture.ChainId);
                account.NonceService = nonceProvider;
                var web3 = new Web3.Web3(account, client);
                // Wait for task completion synchronously in order to Parallel.ForEach work correctly
                var txn = web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000),
                        null, multiplier).Result;
                concurrentDictionary.TryAdd(item, txn);
            });

            var web31 = new Web3.Web3(new Account(privateKey), client);
            var pollService = new TransactionReceiptPollingService(web31.TransactionManager);

            for (var i = 0; i < listTasks; i++)
            {
                string txn = null;
                concurrentDictionary.TryGetValue(i, out txn);
                var receipt = await pollService.PollForReceiptAsync(txn).ConfigureAwait(false);
                Assert.NotNull(receipt);
            }
        }


        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnMultipleWeb3sSingleThreaded()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var client = _ethereumClientIntegrationFixture.GetClient();
            var nonceProvider = new InMemoryNonceService(senderAddress, client);
            var account = new Account(privateKey, EthereumClientIntegrationFixture.ChainId) {NonceService = nonceProvider};
            var web31 = new Web3.Web3(account, client);

            var txn1 = await
                web31.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var web32 = new Web3.Web3(account, client);


            var txn2 = await
                web32.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var web33 = new Web3.Web3(account, client);

            var txn3 = await
                web33.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var pollService = new TransactionReceiptPollingService(web31.TransactionManager);

            var receipt1 = await pollService.PollForReceiptAsync(txn1).ConfigureAwait(false);
            var receipt2 = await pollService.PollForReceiptAsync(txn2).ConfigureAwait(false);
            var receipt3 = await pollService.PollForReceiptAsync(txn3).ConfigureAwait(false);

            Assert.NotNull(receipt1);
            Assert.NotNull(receipt2);
            Assert.NotNull(receipt3);
        }


        [Fact]
        public async void ShouldBeAbleToHandleNoncesOfMultipleTxnSingleWeb3SingleThreaded()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = new Web3.Web3(new Account(privateKey, EthereumClientIntegrationFixture.ChainId), _ethereumClientIntegrationFixture.GetClient());

            var txn1 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var txn2 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var txn3 = await
                web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), null,
                    multiplier).ConfigureAwait(false);

            var pollService = new TransactionReceiptPollingService(web3.TransactionManager);

            var receipt1 = await pollService.PollForReceiptAsync(txn1).ConfigureAwait(false);
            var receipt2 = await pollService.PollForReceiptAsync(txn2).ConfigureAwait(false);
            var receipt3 = await pollService.PollForReceiptAsync(txn3).ConfigureAwait(false);

            Assert.NotNull(receipt1);
            Assert.NotNull(receipt2);
            Assert.NotNull(receipt3);
        }

        [Fact]
        public async Task Given_ASendTheNodeRejects_When_TheNextTransactionIsSent_Then_ItTakesTheReleasedNonceAndIsMined()
        {
            var funder = _ethereumClientIntegrationFixture.GetWeb3();
            var account = new Account(EthECKey.GenerateKey(), EthereumClientIntegrationFixture.ChainId);
            await funder.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(account.Address, 1m).ConfigureAwait(false);

            var web3 = new Web3.Web3(account, _ethereumClientIntegrationFixture.GetClient());
            await Assert.ThrowsAsync<RpcResponseException>(() => web3.TransactionManager.SendTransactionAsync(
                new TransactionInput
                {
                    From = account.Address,
                    To = account.Address,
                    Gas = new HexBigInteger(900),
                    Value = new HexBigInteger(0)
                })).ConfigureAwait(false);

            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var receipt = await web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(EthereumClientIntegrationFixture.AccountAddress, 0.1m,
                    cancellationToken: deadline.Token)
                .ConfigureAwait(false);

            var mined = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(receipt.TransactionHash)
                .ConfigureAwait(false);
            Assert.Equal(0, (int)mined.Nonce.Value);
        }

        private sealed class StubKzgProvider : Nethereum.Model.IBlobKzgProvider
        {
            public byte[] BlobToKzgCommitment(byte[] blob) => new byte[48];
            public byte[] ComputeBlobKzgProof(byte[] blob, byte[] commitment) => new byte[48];
            public byte[] ComputeVersionedHash(byte[] commitment)
            {
                var hash = new byte[32];
                hash[0] = 1;
                return hash;
            }
        }

        [Fact]
        public async Task Given_ASendWithAnExplicitNonceTheNodeRejects_When_ItFails_Then_TheNonceServiceIsLeftAlone()
        {
            var account = new Account(EthECKey.GenerateKey(), EthereumClientIntegrationFixture.ChainId);
            var client = _ethereumClientIntegrationFixture.GetClient();
            var nonceService = new InMemoryNonceService(account.Address, client) { CurrentNonce = 5 };
            account.NonceService = nonceService;
            var web3 = new Web3.Web3(account, client);

            await Assert.ThrowsAsync<RpcResponseException>(() => web3.TransactionManager.SendTransactionAsync(
                new TransactionInput
                {
                    From = account.Address,
                    To = account.Address,
                    Gas = new HexBigInteger(900),
                    Value = new HexBigInteger(0),
                    Nonce = new HexBigInteger(0)
                })).ConfigureAwait(false);

            Assert.Equal(5, (int)nonceService.CurrentNonce);
        }

        [Fact]
        public async Task Given_ABlobSendTheNodeRejects_When_ItFails_Then_TheNonceServiceIsReset()
        {
            var account = new Account(EthECKey.GenerateKey(), EthereumClientIntegrationFixture.ChainId);
            var client = _ethereumClientIntegrationFixture.GetClient();
            var nonceService = new InMemoryNonceService(account.Address, client);
            account.NonceService = nonceService;
            var web3 = new Web3.Web3(account, client);

            await Assert.ThrowsAsync<RpcResponseException>(() =>
                ((AccountSignerTransactionManager)web3.TransactionManager).SendBlobTransactionAsync(
                    new byte[] { 1, 2, 3 }, account.Address, new StubKzgProvider(), new HexBigInteger(1)))
                .ConfigureAwait(false);

            Assert.Equal(-1, (int)nonceService.CurrentNonce);
        }
    }
}
