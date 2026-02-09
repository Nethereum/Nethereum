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
            // Given: An OwnableValidator contract
            var validatorService = await GetOwnableValidatorServiceAsync();

            // When: Checking if it's a validator type module
            var isValidator = await validatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: It confirms it's a validator
            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_CheckingModuleType7_Then_ReturnsTrueForK1Validator()
        {
            // Given: An OwnableValidator contract
            var validatorService = await GetOwnableValidatorServiceAsync();

            // When: Checking module type 7 (ERC-7579 K1 Validator type)
            var isK1Validator = await validatorService.IsModuleTypeQueryAsync(7);

            // Then: It confirms it's a K1 validator
            Assert.True(isK1Validator);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_QueryingName_Then_ReturnsOwnableValidator()
        {
            // Given: An OwnableValidator contract
            var validatorService = await GetOwnableValidatorServiceAsync();

            // When: Querying the name
            var name = await validatorService.NameQueryAsync();

            // Then: Name is OwnableValidator
            Assert.Equal("OwnableValidator", name);
        }

        [Fact]
        public async Task Given_OwnableValidator_When_QueryingVersion_Then_ReturnsValidVersion()
        {
            // Given: An OwnableValidator contract
            var validatorService = await GetOwnableValidatorServiceAsync();

            // When: Querying the version
            var version = await validatorService.VersionQueryAsync();

            // Then: Version is valid
            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public void Given_OwnableValidatorConfig_When_BuildingWithFluentAPI_Then_ProducesCorrectConfig()
        {
            // Given: A fluent configuration builder
            var moduleAddress = "0x1234567890123456789012345678901234567890";
            var owner1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var owner2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            // When: Building config with chained methods
            var config = new OwnableValidatorConfig { ModuleAddress = moduleAddress }
                .WithThreshold(2)
                .WithOwner(owner1)
                .WithOwner(owner2);

            // Then: Config is properly constructed
            Assert.Equal(2, config.Threshold);
            Assert.Equal(2, config.Owners.Count);
            Assert.Contains(owner1, config.Owners);
            Assert.Contains(owner2, config.Owners);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_OwnableValidatorConfig_When_GettingInitData_Then_ReturnsEncodedData()
        {
            // Given: A valid config
            var config = OwnableValidatorConfig.Create(
                "0x1234567890123456789012345678901234567890",
                threshold: 2,
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            // When: Getting init data
            var initData = config.GetInitData();

            // Then: Init data is properly encoded (threshold + owners array)
            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }

        [Fact]
        public void Given_InvalidThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            // Given: A config with threshold greater than owners
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 3
            }
            .WithOwner("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .WithOwner("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_NoOwners_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            // Given: A config with no owners
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 1
            };

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_ZeroThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            // Given: A config with zero threshold
            var config = new OwnableValidatorConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 0
            }
            .WithOwner("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }
    }
}
