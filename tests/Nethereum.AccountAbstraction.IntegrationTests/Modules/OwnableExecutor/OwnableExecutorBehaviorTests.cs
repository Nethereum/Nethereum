using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.OwnableExecutor
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "OwnableExecutor")]
    public class OwnableExecutorBehaviorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private OwnableExecutorService _ownableExecutorService;

        public OwnableExecutorBehaviorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<OwnableExecutorService> GetOwnableExecutorServiceAsync()
        {
            if (_ownableExecutorService == null)
            {
                _ownableExecutorService = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                    _fixture.Web3, new OwnableExecutorDeployment());
            }
            return _ownableExecutorService;
        }

        [Fact]
        public async Task Given_OwnableExecutor_When_CheckingModuleType_Then_ReturnsExecutorType()
        {
            var executorService = await GetOwnableExecutorServiceAsync();

            var isExecutor = await executorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.True(isExecutor);
        }

        [Fact]
        public async Task Given_OwnableExecutor_When_CheckingValidatorType_Then_ReturnsFalse()
        {
            var executorService = await GetOwnableExecutorServiceAsync();

            var isValidator = await executorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.False(isValidator);
        }

        [Fact]
        public async Task Given_OwnableExecutor_When_QueryingName_Then_ReturnsOwnableExecutor()
        {
            var executorService = await GetOwnableExecutorServiceAsync();

            var name = await executorService.NameQueryAsync();

            Assert.Equal("OwnableExecutor", name);
        }

        [Fact]
        public async Task Given_OwnableExecutor_When_QueryingVersion_Then_ReturnsValidVersion()
        {
            var executorService = await GetOwnableExecutorServiceAsync();

            var version = await executorService.VersionQueryAsync();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public void Given_OwnableExecutorConfig_When_CreatingInitData_Then_ReturnsOwnerAddress()
        {
            var ownerAddress = "0x1234567890123456789012345678901234567890";
            var config = new OwnableExecutorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Owner = ownerAddress
            };

            var initData = config.GetInitData();

            Assert.Equal(20, initData.Length);
            Assert.Equal(ownerAddress.ToLower(), ("0x" + initData.ToHex()).ToLower());
        }

        [Fact]
        public void Given_OwnableExecutorConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var ownerAddress = "0x1234567890123456789012345678901234567890";

            var config = OwnableExecutorConfig.Create(moduleAddress, ownerAddress);

            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(ownerAddress, config.Owner);
            Assert.Equal(ERC7579ModuleTypes.TYPE_EXECUTOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_OwnableExecutorConfigWithNoOwner_When_GettingInitData_Then_Throws()
        {
            var config = new OwnableExecutorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            };

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public async Task Given_Account_When_CheckingSupportsExecutorModule_Then_ReturnsTrue()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var supportsExecutor = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.True(supportsExecutor);
        }
    }
}
