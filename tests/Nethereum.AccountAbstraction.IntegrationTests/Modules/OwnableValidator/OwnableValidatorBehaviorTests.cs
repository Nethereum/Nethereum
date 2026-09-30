using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.OwnableValidator
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "OwnableValidator")]
    public class OwnableValidatorBehaviorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private OwnableValidatorService _ownableValidatorService;

        public OwnableValidatorBehaviorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<OwnableValidatorService> GetOwnableValidatorServiceAsync()
        {
            if (_ownableValidatorService == null)
            {
                _ownableValidatorService = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                    _fixture.Web3, new OwnableValidatorDeployment());
            }
            return _ownableValidatorService;
        }

        [Fact]
        public async Task Given_OwnableValidator_When_CheckingModuleType_Then_ReturnsValidatorType()
        {
            var validatorService = await GetOwnableValidatorServiceAsync();

            var isValidator = await validatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_CheckingModuleType7_Then_ReturnsTrueForK1Validator()
        {
            var validatorService = await GetOwnableValidatorServiceAsync();

            var isK1Validator = await validatorService.IsModuleTypeQueryAsync(7);

            Assert.True(isK1Validator);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_QueryingName_Then_ReturnsOwnableValidator()
        {
            var validatorService = await GetOwnableValidatorServiceAsync();

            var name = await validatorService.NameQueryAsync();

            Assert.Equal("OwnableValidator", name);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_QueryingVersion_Then_ReturnsValidVersion()
        {
            var validatorService = await GetOwnableValidatorServiceAsync();

            var version = await validatorService.VersionQueryAsync();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public void Given_OwnableValidatorConfig_When_BuildingWithFluentAPI_Then_ProducesCorrectConfig()
        {
            var moduleAddress = "0x1234567890123456789012345678901234567890";
            var owner1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var owner2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            var config = new OwnableValidatorConfig { ModuleAddress = moduleAddress }
                .WithThreshold(2)
                .WithOwner(owner1)
                .WithOwner(owner2);

            Assert.Equal(2, config.Threshold);
            Assert.Equal(2, config.Owners.Count);
            Assert.Contains(owner1, config.Owners);
            Assert.Contains(owner2, config.Owners);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_OwnableValidatorConfig_When_GettingInitData_Then_ReturnsEncodedData()
        {
            var config = OwnableValidatorConfig.Create(
                "0x1234567890123456789012345678901234567890",
                threshold: 2,
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            var initData = config.GetInitData();

            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }

        [Fact]
        public void Given_InvalidThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 3
            }
            .WithOwner("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .WithOwner("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_NoOwners_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 1
            };

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_ZeroThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 0
            }
            .WithOwner("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }
    }
}
