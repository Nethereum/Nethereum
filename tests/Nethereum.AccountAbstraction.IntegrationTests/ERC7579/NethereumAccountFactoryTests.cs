using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.ERC7579
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class NethereumAccountFactoryTests
    {
        private readonly ERC7579TestFixture _fixture;

        public NethereumAccountFactoryTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DeployFactory_WithValidEntryPoint_Succeeds()
        {
            var deployment = new NethereumAccountFactoryDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };

            var service = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var entryPoint = await _fixture.AccountFactoryService.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetAccountImplementation_ReturnsValidAddress()
        {
            var implementation = await _fixture.AccountFactoryService.AccountImplementationQueryAsync();

            Assert.NotNull(implementation);
            Assert.StartsWith("0x", implementation);
            Assert.Equal(42, implementation.Length);
        }

        [Fact]
        public async Task GetAddress_ReturnsDeterministicAddress()
        {
            var salt = _fixture.CreateSalt(1);
            var initData = _fixture.CreateInitData();

            var address1 = await _fixture.AccountFactoryService.GetAddressQueryAsync(salt, initData);
            var address2 = await _fixture.AccountFactoryService.GetAddressQueryAsync(salt, initData);

            Assert.Equal(address1.ToLower(), address2.ToLower());
        }

        [Fact]
        public async Task GetAddress_DifferentSalts_ReturnsDifferentAddresses()
        {
            var salt1 = _fixture.CreateSalt(100);
            var salt2 = _fixture.CreateSalt(200);
            var initData = _fixture.CreateInitData();

            var address1 = await _fixture.AccountFactoryService.GetAddressQueryAsync(salt1, initData);
            var address2 = await _fixture.AccountFactoryService.GetAddressQueryAsync(salt2, initData);

            Assert.NotEqual(address1.ToLower(), address2.ToLower());
        }

        [Fact]
        public async Task IsDeployed_BeforeCreation_ReturnsFalse()
        {
            var salt = _fixture.CreateSalt(9999);
            var initData = _fixture.CreateInitData();

            var isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(salt, initData);

            Assert.False(isDeployed);
        }

        [Fact]
        public async Task CreateAccount_DeploysAccount()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var initData = _fixture.CreateInitData();

            var receipt = await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);

            Assert.NotNull(receipt);
            Assert.Equal(1, (int)receipt.Status.Value);

            var isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(salt, initData);
            Assert.True(isDeployed);
        }

        [Fact]
        public async Task CreateAccount_ReturnsCorrectAddress()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1);
            var initData = _fixture.CreateInitData();

            var predictedAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(salt, initData);
            await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);

            var isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(salt, initData);
            Assert.True(isDeployed);

            var code = await _fixture.Web3.Eth.GetCode.SendRequestAsync(predictedAddress);
            Assert.NotEqual("0x", code);
        }

        [Fact]
        public async Task GetInitCode_ReturnsValidInitCode()
        {
            var salt = _fixture.CreateSalt(500);
            var initData = _fixture.CreateInitData();

            var initCode = await _fixture.AccountFactoryService.GetInitCodeQueryAsync(salt, initData);

            Assert.NotNull(initCode);
            Assert.True(initCode.Length > 0);
        }
    }
}
