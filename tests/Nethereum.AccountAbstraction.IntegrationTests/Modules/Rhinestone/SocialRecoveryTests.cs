using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.Rhinestone
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class SocialRecoveryTests
    {
        private readonly ERC7579TestFixture _fixture;
        private SocialRecoveryService? _recoveryService;

        public SocialRecoveryTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SocialRecoveryService> GetOrDeployRecoveryAsync()
        {
            if (_recoveryService != null)
                return _recoveryService;

            _recoveryService = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SocialRecoveryDeployment());

            return _recoveryService;
        }

        [Fact]
        public async Task DeploySocialRecovery_Succeeds()
        {
            var service = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SocialRecoveryDeployment());

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task GetName_ReturnsValidName()
        {
            var recovery = await GetOrDeployRecoveryAsync();

            var name = await recovery.NameQueryAsync();

            Assert.NotNull(name);
            Assert.NotEmpty(name);
        }

        [Fact]
        public async Task GetVersion_ReturnsValidVersion()
        {
            var recovery = await GetOrDeployRecoveryAsync();

            var version = await recovery.VersionQueryAsync();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public async Task IsModuleType_Validator_ReturnsTrue()
        {
            var recovery = await GetOrDeployRecoveryAsync();

            var isValidator = await recovery.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task IsModuleType_Executor_ReturnsFalse()
        {
            var recovery = await GetOrDeployRecoveryAsync();

            var isExecutor = await recovery.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.False(isExecutor);
        }

        [Fact]
        public async Task IsInitialized_UnknownAccount_ReturnsFalse()
        {
            var recovery = await GetOrDeployRecoveryAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var isInitialized = await recovery.IsInitializedQueryAsync(unknownAccount);

            Assert.False(isInitialized);
        }

        [Fact]
        public async Task GetGuardians_UnknownAccount_ReturnsEmptyList()
        {
            var recovery = await GetOrDeployRecoveryAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var guardians = await recovery.GetGuardiansQueryAsync(unknownAccount);

            Assert.NotNull(guardians);
            Assert.Empty(guardians);
        }

        [Fact]
        public async Task GuardianCount_UnknownAccount_ReturnsZero()
        {
            var recovery = await GetOrDeployRecoveryAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var count = await recovery.GuardianCountQueryAsync(unknownAccount);

            Assert.Equal(BigInteger.Zero, count);
        }

        [Fact]
        public async Task Threshold_UnknownAccount_ReturnsZero()
        {
            var recovery = await GetOrDeployRecoveryAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var threshold = await recovery.ThresholdQueryAsync(unknownAccount);

            Assert.Equal(BigInteger.Zero, threshold);
        }
    }
}
