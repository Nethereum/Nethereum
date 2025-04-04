using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory;
using Nethereum.AccountAbstraction.IntegrationTests.TestUtil;
using Nethereum.AccountAbstraction.IntegrationTests.TestUtil.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class SimpleAccountTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public SimpleAccountTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public async Task<string> CreateSimpleAccountAsync(string ownerAddress, string entryPointAddress)
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var simpleAccountFactoryDeployment = new SimpleAccountFactoryDeployment();
            simpleAccountFactoryDeployment.EntryPoint = entryPointAddress;
            var simpleAccountFactoryService =
                await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(web3, simpleAccountFactoryDeployment);
            var txnAccountCreationReceipt = await simpleAccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(ownerAddress, 0);
            //0 salt
            return await simpleAccountFactoryService.CreateAccountQueryAsync(ownerAddress, 0);
        }

        [Fact]
        public async Task ShouldX()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;

            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());
            var accountAddress = await CreateSimpleAccountAsync(ownerAddress, entryPointService.ContractHandler.ContractAddress);
            var simpleAccountService = new SimpleAccountService(web3, accountAddress);

            await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(accountAddress, 2);

            var receipt = await simpleAccountService.ExecuteRequestAndWaitForReceiptAsync(new ExecuteFunction
            {
                Data = new byte[0],
                Value = Web3.Web3.Convert.ToWei(1),
                Target = "0xb1ac840891b22e2892cF53B681cefA75d987AEAc"
            });

            var balance = await web3.Eth.GetBalance.SendRequestAsync("0xb1ac840891b22e2892cF53B681cefA75d987AEAc");
            var balanceInEth = Web3.Web3.Convert.FromWei(balance.Value);
            Assert.Equal(1, balanceInEth);

        }

        [Fact]
        public async Task ShouldPackInCSharpTheSameAsSolidity()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;
            var testUtilService = await TestUtilService.DeployContractAndGetServiceAsync(web3, new TestUtilDeployment());
            var userOperation = new UserOperation();
            userOperation.SetNullValuesToDefaultValues();

            var packedUserOperation = UserOperationBuilder.PackUserOperation(userOperation);

            var packedUserOperationStruct = new TestUtil.ContractDefinition.PackedUserOperation
            {
                Sender = packedUserOperation.Sender,
                CallData = packedUserOperation.CallData,
                PreVerificationGas = packedUserOperation.PreVerificationGas,
                AccountGasLimits = packedUserOperation.AccountGasLimits,
                InitCode = packedUserOperation.InitCode,
                PaymasterAndData = packedUserOperation.PaymasterAndData,
                GasFees = packedUserOperation.GasFees,
                Signature = packedUserOperation.Signature,
                Nonce = packedUserOperation.Nonce
            };

            var encoded = UserOperationBuilder.PackAndEncodeUserOperationStruct(userOperation);

            var encodedSolidity = await testUtilService.EncodeUserOpQueryAsync(packedUserOperationStruct);
            Assert.True(encoded.ToHex().IsTheSameHex(encodedSolidity.ToHex()));
        }

    }

}
