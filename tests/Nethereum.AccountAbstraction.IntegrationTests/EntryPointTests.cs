using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount;
using Nethereum.Signer;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests
{


    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EntryPointTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EntryPointTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }


        [Fact]
        public async Task ShouldCreateAccountAfterPrefundAsync()
        {
            ulong salt = 20;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;
            var ethKey = new Nethereum.Signer.EthECKey(privateKey);

            var accountOwner = EthereumClientIntegrationFixture.AccountAddress;
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());
            
            var simpleAccountFactoryDeployment = new SimpleAccountFactoryDeployment();
            simpleAccountFactoryDeployment.EntryPoint = entryPointService.ContractAddress;
            
            var simpleAccountFactoryService =
                await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(web3, simpleAccountFactoryDeployment);

            var accountCreateResult = await simpleAccountFactoryService.CreateAndDeployAccountAsync(
                accountOwner,
                accountOwner,
                entryPointService.ContractAddress,
                ethKey,
                0.01m,
                salt
                );

            var accountAddress = accountCreateResult.AccountAddress;
            Assert.NotNull(accountAddress);
            Assert.NotNull(accountCreateResult.Receipt);

        }

        [Fact]
        public async Task ShouldExecuteAsync()
        {

            ulong salt = 20;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var privateKey = EthereumClientIntegrationFixture.AccountPrivateKey;
            var ethKey = new Nethereum.Signer.EthECKey(privateKey);

            var accountOwner = EthereumClientIntegrationFixture.AccountAddress;
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var simpleAccountFactoryDeployment = new SimpleAccountFactoryDeployment();
            simpleAccountFactoryDeployment.EntryPoint = entryPointService.ContractAddress;

            var simpleAccountFactoryService =
                await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(web3, simpleAccountFactoryDeployment);

            var accountCreateResult = await simpleAccountFactoryService.CreateAndDeployAccountAsync(
                accountOwner,
                accountOwner,
                entryPointService.ContractAddress,
                ethKey,
                0.01m,
                salt
                );

            var accountAddress = accountCreateResult.AccountAddress;

            var counterService = await TestCounterService.DeployContractAndGetServiceAsync(web3, new TestCounterDeployment());

            var countFunction = new CountFunction();
            var executeFunction = new ExecuteFunction()
            {
                Target = counterService.ContractAddress,
                Value = 0,
                Data = countFunction.GetCallData()
            };

            var op = await entryPointService.SignAndInitialiseUserOperationAsync(new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 2_000_000,
                VerificationGasLimit = 76_000
            }, ethKey);

            var handleOpsRequest = new HandleOpsFunction()
            {
                Ops = new List<EntryPoint.ContractDefinition.PackedUserOperation>() { op },
                Beneficiary = accountOwner,
                Gas = 10_000_000
            };

            var receipt = await entryPointService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsRequest);

            var count1 = await counterService.CountersQueryAsync(accountAddress);
          

            Assert.Equal(1, count1);
        }


        [Fact]
        public async Task ShouldExecuteBatchMultipleRequestsAsync()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var beneficiaryAddress = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";

            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var factory = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(web3,
                new SimpleAccountFactoryDeployment { EntryPoint = entryPoint.ContractAddress });

           

            // Deploy TestCounter contract
            var counterService = await TestCounterService.DeployContractAndGetServiceAsync(web3, new TestCounterDeployment());

            // Prepare calldata for counter.count()
            var countCall = new CountFunction();

            var accountOwner2 = TestAccounts.Account2Address;
            var accountOwner3 = TestAccounts.Account3Address;
            var accountOwner2Key = new EthECKey(TestAccounts.Account2PrivateKey);
            var accountOwner3Key = new EthECKey(TestAccounts.Account3PrivateKey);

            

            var account2Address = await factory.GetAddressQueryAsync(accountOwner2, 999);
            //var account2 = await factory.CreateAndDeployAccountAsync(accountOwner2, accountOwner2,
            //    entryPoint.ContractAddress, accountOwner2Key, 0.01m, 888);

            var account3Address = await factory.GetAddressQueryAsync(accountOwner3, 999);
            var account3 = await factory.CreateAndDeployAccountAsync(accountOwner3, accountOwner3,
                entryPoint.ContractAddress, accountOwner3Key, 0.01m, 999);


            //Fund the 4337 accounts

            //this one will be created on the first op
            var fundTx = await web3.Eth.GetEtherTransferService()
              .TransferEtherAndWaitForReceiptAsync(account2Address, 0.02m);

            //this one has been already created (and funded with 0.01) but just adding more funds
            fundTx = await web3.Eth.GetEtherTransferService()
              .TransferEtherAndWaitForReceiptAsync(account3.AccountAddress, 0.02m);

            var countFunction = new CountFunction();
            var executeFunction = new ExecuteFunction()
            {
                Target = counterService.ContractAddress,
                Value = 0,
                Data = countFunction.GetCallData()
            };
           

            // Operation 1: create + execute
            var initCode1 = factory.GetCreateAccountInitCode(accountOwner2, 999);
            var op1 = await entryPoint.SignAndInitialiseUserOperationAsync(new UserOperation
            {
                InitCode = initCode1,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 2_000_000,
                VerificationGasLimit = 2_000_000
            }, accountOwner2Key);

            // Operation 2: from deployed account
            var op2 = await entryPoint.SignAndInitialiseUserOperationAsync(new UserOperation
            {
                Sender = account3Address,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 2_000_000,
                VerificationGasLimit = 76_000
            }, accountOwner3Key);


            try
            {
                // Static call check
                await entryPoint.HandleOpsQueryAsync(new HandleOpsFunction
                {
                    Ops = new List<EntryPoint.ContractDefinition.PackedUserOperation> { op1, op2 },
                    Beneficiary = beneficiaryAddress,
                    Gas = 10_000_000
                });

            }
            catch(SmartContractCustomErrorRevertException ex)
            {
               var error = entryPoint.FindCustomErrorException(ex);
               throw error;
            }


            // Execute both
            var receipt = await entryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<EntryPoint.ContractDefinition.PackedUserOperation> { op1, op2 },
                Beneficiary = beneficiaryAddress,
                Gas = 10_000_000
            });

            var count1 = await counterService.CountersQueryAsync(account2Address);
            var count2 = await counterService.CountersQueryAsync(account3Address);

            Assert.Equal(1, count1);
            Assert.Equal(1, count2);
        }

    }
}
