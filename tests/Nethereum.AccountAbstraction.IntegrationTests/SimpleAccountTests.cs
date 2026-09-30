using Nethereum.Contracts;
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
using Nethereum.ABI.FunctionEncoding;

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
            return await simpleAccountFactoryService.CreateAccountQueryAsync(ownerAddress, 0);
        }

        [Fact]
        public async Task ShouldThrowWhenTryingToCreateAnAccountDirectly()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;

            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());
            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
            {
                var accountAddress = await CreateSimpleAccountAsync(ownerAddress, entryPointService.ContractHandler.ContractAddress);
            });

            Assert.True(ex.IsCustomErrorFor<Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition.NotSenderCreatorError>());
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

            var encoded = UserOperationBuilder.PackAndEncodeUserOperationStruct(userOperation);

            var encodedSolidity = await testUtilService.EncodeUserOpQueryAsync(packedUserOperation);
            Assert.True(encoded.ToHex().IsTheSameHex(encodedSolidity.ToHex()));
        }

    }

}
