using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SocialRecovery
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "SocialRecovery")]
    public class SocialRecoveryBehaviorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private SocialRecoveryService _socialRecoveryService;

        public SocialRecoveryBehaviorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SocialRecoveryService> GetSocialRecoveryServiceAsync()
        {
            if (_socialRecoveryService == null)
            {
                _socialRecoveryService = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                    _fixture.Web3, new SocialRecoveryDeployment());
            }
            return _socialRecoveryService;
        }

        [Fact]
        public async Task Given_SocialRecovery_When_CheckingModuleType_Then_ReturnsValidatorType()
        {
            // Given: A SocialRecovery contract (it's a validator module)
            var recoveryService = await GetSocialRecoveryServiceAsync();

            // When: Checking if it's a validator type module
            var isValidator = await recoveryService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: It confirms it's a validator
            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_SocialRecovery_When_QueryingName_Then_ReturnsSocialRecoveryValidator()
        {
            // Given: A SocialRecovery contract
            var recoveryService = await GetSocialRecoveryServiceAsync();

            // When: Querying the name
            var name = await recoveryService.NameQueryAsync();

            // Then: Name contains SocialRecovery
            Assert.Contains("SocialRecovery", name);
        }

        [Fact]
        public async Task Given_SocialRecovery_When_QueryingVersion_Then_ReturnsValidVersion()
        {
            // Given: A SocialRecovery contract
            var recoveryService = await GetSocialRecoveryServiceAsync();

            // When: Querying the version
            var version = await recoveryService.VersionQueryAsync();

            // Then: Version is valid
            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_BuildingWithFluentAPI_Then_ProducesCorrectConfig()
        {
            // Given: A fluent configuration builder
            var moduleAddress = "0x1234567890123456789012345678901234567890";
            var guardian1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var guardian2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            // When: Building config with chained methods
            var config = new SocialRecoveryConfig { ModuleAddress = moduleAddress }
                .WithThreshold(2)
                .WithGuardian(guardian1)
                .WithGuardian(guardian2);

            // Then: Config is properly constructed
            Assert.Equal(2, config.Threshold);
            Assert.Equal(2, config.Guardians.Count);
            Assert.Contains(guardian1, config.Guardians);
            Assert.Contains(guardian2, config.Guardians);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_GettingInitData_Then_ReturnsEncodedData()
        {
            // Given: A valid config
            var config = SocialRecoveryConfig.Create(
                "0x1234567890123456789012345678901234567890",
                threshold: 2,
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            // When: Getting init data
            var initData = config.GetInitData();

            // Then: Init data is properly encoded (threshold + guardians array)
            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }

        [Fact]
        public void Given_InvalidThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            // Given: A config with threshold greater than guardians
            var config = new SocialRecoveryConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 3
            }
            .WithGuardian("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .WithGuardian("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_NoGuardians_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            // Given: A config with no guardians
            var config = new SocialRecoveryConfig
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
            var config = new SocialRecoveryConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 0
            }
            .WithGuardian("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            // Given: Config created via static method
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var guardian1 = "0x1111111111111111111111111111111111111111";
            var guardian2 = "0x2222222222222222222222222222222222222222";
            var guardian3 = "0x3333333333333333333333333333333333333333";

            // When: Using static Create method
            var config = SocialRecoveryConfig.Create(moduleAddress, 2, guardian1, guardian2, guardian3);

            // Then: Config is properly set
            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(2, config.Threshold);
            Assert.Equal(3, config.Guardians.Count);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }
    }
}
