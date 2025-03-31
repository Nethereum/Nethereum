using Nethereum.Contracts;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer.IntegrationTests.BatchCallAndSponsor;
using Nethereum.Signer.IntegrationTests.BatchCallAndSponsor.ContractDefinition;
using Nethereum.Signer.IntegrationTests.MockERC20;
using Nethereum.Signer.IntegrationTests.MockERC20.ContractDefinition;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Signer.IntegrationTests
{


   // [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EIP7022Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        //public EIP7022Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        //{
        //    //_ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        //}

        [Fact]
        public async Task ShouldCreateEip7022AuthorisationAndExecuteDelegatedSmartContractAsync()
        {
            //this uses for the time being foundry anvil --init genesis.json -v --hardfork prague 
            //or sepolia until using internal geth node for testing
            //the testing smart contract is the BatchCallAndSponsor from https://github.com/quiknode-labs/qn-guide-examples

            var receiverAddress = "0x111f530216fbb0377b4bdd4d303a465a1090d09d";

            var account = new Nethereum.Web3.Accounts.Account(EthereumClientIntegrationFixture.AccountPrivateKey, 1);
            var web3 = new Web3.Web3(account);
            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;

            var defaultBatchCallService = await BatchCallAndSponsorService.DeployContractAndGetServiceAsync(web3, new BatchCallAndSponsorDeployment());
            var tokenService = await MockERC20Service.DeployContractAndGetServiceAsync(web3, new MockERC20Deployment());
            await tokenService.MintRequestAndWaitForReceiptAsync(ownerAddress, 1000000);

            //var defaultBachCallServiceAddress = "0x976d1885a5d42ccfe5327a06c8d6b6d519fde365";
            //var tokenAddress = "0x4c03e0ac1fa9e70116ab63b5c9db5a8ff0d2d2e5";
            //var web3 = new Web3.Web3(account, "https://ethereum-sepolia-rpc.publicnode.com");

            //var defaultBatchCallService = new BatchCallAndSponsorService(web3, defaultBachCallServiceAddress);
            //var tokenService = new MockERC20Service(web3, tokenAddress);

           // var ownerAddress = "0x426f99291Ba267E155d9B9B4Fa5d698A23DdE108"; //EthereumClientIntegrationFixture.AccountAddress;

            
           var authorisationService = web3.Eth.GetEIP7022AuthorisationService();
            var removeReceipt = await authorisationService.RemoveAuthorisationRequestAndWaitForReceiptAsync();

           var delegatedAddress = await authorisationService.GetDelegatedAccountAddressAsync(ownerAddress);
           Assert.Null(delegatedAddress);

           var authorisationReceipt = await authorisationService.AuthoriseRequestAndWaitForReceiptAsync(defaultBatchCallService.ContractAddress, true);

           var transaction = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(authorisationReceipt.TransactionHash);
           
           Assert.True(authorisationReceipt.Status.Value == 1);
           Assert.NotNull(transaction);
           Assert.True(transaction.AuthorisationList.Count == 1);
           Assert.Equal(0, transaction.AuthorisationList[0].ChainId.Value);
           Assert.Equal(defaultBatchCallService.ContractAddress, transaction.AuthorisationList[0].Address);

           delegatedAddress = await authorisationService.GetDelegatedAccountAddressAsync(ownerAddress);
           Assert.True(defaultBatchCallService.ContractAddress.IsTheSameAddress(delegatedAddress));

           var originalReceiverTokenBalance = await tokenService.BalanceOfQueryAsync(receiverAddress);
           var originalReceiverEthBalance = await web3.Eth.GetBalance.SendRequestAsync(receiverAddress);



            var calls = new List<Call>();
            var call1 = new Call();
            call1.To = receiverAddress;
            call1.Data = new byte[0];
            call1.Value = Web3.Web3.Convert.ToWei(0.0001);
            calls.Add(call1);

            var call2 = new Call();
            call2.To = tokenService.ContractHandler.ContractAddress;
            call2.Value = 0;
            call2.Data = new TransferFunction() { To = receiverAddress, Value = 100 }.GetCallData();
            calls.Add(call2);


            
            var accountBatchService = new BatchCallAndSponsorService(web3,ownerAddress);
            var receipt = await accountBatchService.ExecuteRequestAndWaitForReceiptAsync(calls);
            var batchExecutedEvent = receipt.DecodeAllEvents<BatchExecutedEventDTO>().FirstOrDefault();

            var receiverEthBalance = await web3.Eth.GetBalance.SendRequestAsync(receiverAddress);
            Assert.Equal(originalReceiverEthBalance + Web3.Web3.Convert.ToWei(0.0001), receiverEthBalance);

            var receiverTokenBalance = await tokenService.BalanceOfQueryAsync(receiverAddress);
            Assert.Equal(originalReceiverTokenBalance + 100, receiverTokenBalance);


            var removeReceipt2 = await authorisationService.RemoveAuthorisationRequestAndWaitForReceiptAsync();
            delegatedAddress = await authorisationService.GetDelegatedAccountAddressAsync(ownerAddress);
            Assert.Null(delegatedAddress);

            //start again.. but now with the authorisation at the same time as submitting the transaction

            originalReceiverTokenBalance = await tokenService.BalanceOfQueryAsync(receiverAddress);
            originalReceiverEthBalance = await web3.Eth.GetBalance.SendRequestAsync(receiverAddress);

            

            var executionRequest = new ExecuteFunction();
            executionRequest.Calls = calls;
            executionRequest.Gas = 1000000; //as we are adding the authorisation on the same request we need to set the gas now ... estimates are not possible
            executionRequest.AuthorisationList = new List<Authorisation>
            {
                new Authorisation() { Address = defaultBatchCallService.ContractAddress }
            };
            //we could have done this too.. instead of adding the authorisation on the function message
            //await authorisationService.Add7022AuthorisationDelegationOnNextRequestAsync(defaultBatchCallService.ContractAddress);
            receipt = await accountBatchService.ExecuteRequestAndWaitForReceiptAsync(executionRequest);
            batchExecutedEvent = receipt.DecodeAllEvents<BatchExecutedEventDTO>().FirstOrDefault();

            receiverEthBalance = await web3.Eth.GetBalance.SendRequestAsync(receiverAddress);
            Assert.Equal(originalReceiverEthBalance + Web3.Web3.Convert.ToWei(0.0001), receiverEthBalance);

            receiverTokenBalance = await tokenService.BalanceOfQueryAsync(receiverAddress);
            Assert.Equal(originalReceiverTokenBalance + 100, receiverTokenBalance);


        }

    }
}
