using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
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
            var recoveryService = await GetSocialRecoveryServiceAsync();

            var isValidator = await recoveryService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_SocialRecovery_When_QueryingName_Then_ReturnsSocialRecoveryValidator()
        {
            var recoveryService = await GetSocialRecoveryServiceAsync();

            var name = await recoveryService.NameQueryAsync();

            Assert.Contains("SocialRecovery", name);
        }

        [Fact]
        public async Task Given_SocialRecovery_When_QueryingVersion_Then_ReturnsValidVersion()
        {
            var recoveryService = await GetSocialRecoveryServiceAsync();

            var version = await recoveryService.VersionQueryAsync();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_BuildingWithFluentAPI_Then_ProducesCorrectConfig()
        {
            var moduleAddress = "0x1234567890123456789012345678901234567890";
            var guardian1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var guardian2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            var config = new SocialRecoveryConfig { ModuleAddress = moduleAddress }
                .WithThreshold(2)
                .WithGuardian(guardian1)
                .WithGuardian(guardian2);

            Assert.Equal(2, config.Threshold);
            Assert.Equal(2, config.Guardians.Count);
            Assert.Contains(guardian1, config.Guardians);
            Assert.Contains(guardian2, config.Guardians);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_GettingInitData_Then_ReturnsEncodedData()
        {
            var config = SocialRecoveryConfig.Create(
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
            var config = new SocialRecoveryConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 3
            }
            .WithGuardian("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .WithGuardian("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_NoGuardians_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            var config = new SocialRecoveryConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 1
            };

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_ZeroThreshold_When_CreatingConfig_Then_ThrowsOnGetInitData()
        {
            var config = new SocialRecoveryConfig
            {
                ModuleAddress = "0x1234567890123456789012345678901234567890",
                Threshold = 0
            }
            .WithGuardian("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public void Given_SocialRecoveryConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var guardian1 = "0x1111111111111111111111111111111111111111";
            var guardian2 = "0x2222222222222222222222222222222222222222";
            var guardian3 = "0x3333333333333333333333333333333333333333";

            var config = SocialRecoveryConfig.Create(moduleAddress, 2, guardian1, guardian2, guardian3);

            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(2, config.Threshold);
            Assert.Equal(3, config.Guardians.Count);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }
    }
}
