using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.Rhinestone
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class OwnableValidatorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private OwnableValidatorService? _validatorService;

        public OwnableValidatorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<OwnableValidatorService> GetOrDeployValidatorAsync()
        {
            if (_validatorService != null)
                return _validatorService;

            _validatorService = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new OwnableValidatorDeployment());

            return _validatorService;
        }

        [Fact]
        public async Task DeployValidator_Succeeds()
        {
            var service = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new OwnableValidatorDeployment());

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task GetName_ReturnsValidName()
        {
            var validator = await GetOrDeployValidatorAsync();

            var name = await validator.NameQueryAsync();

            Assert.NotNull(name);
            Assert.NotEmpty(name);
        }

        [Fact]
        public async Task IsModuleType_Validator_ReturnsTrue()
        {
            var validator = await GetOrDeployValidatorAsync();

            var isValidator = await validator.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task IsModuleType_Executor_ReturnsFalse()
        {
            var validator = await GetOrDeployValidatorAsync();

            var isExecutor = await validator.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.False(isExecutor);
        }

        [Fact]
        public async Task IsInitialized_UnknownAccount_ReturnsFalse()
        {
            var validator = await GetOrDeployValidatorAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var isInitialized = await validator.IsInitializedQueryAsync(unknownAccount);

            Assert.False(isInitialized);
        }

        [Fact]
        public async Task GetOwners_UnknownAccount_ReturnsEmptyList()
        {
            var validator = await GetOrDeployValidatorAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var owners = await validator.GetOwnersQueryAsync(unknownAccount);

            Assert.NotNull(owners);
            Assert.Empty(owners);
        }

        [Fact]
        public async Task OwnerCount_UnknownAccount_ReturnsZero()
        {
            var validator = await GetOrDeployValidatorAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var count = await validator.OwnerCountQueryAsync(unknownAccount);

            Assert.Equal(BigInteger.Zero, count);
        }

        [Fact]
        public async Task Threshold_UnknownAccount_ReturnsZero()
        {
            var validator = await GetOrDeployValidatorAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var threshold = await validator.ThresholdQueryAsync(unknownAccount);

            Assert.Equal(BigInteger.Zero, threshold);
        }
    }
}
