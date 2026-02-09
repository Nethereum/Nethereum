using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class ECDSAValidatorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private ECDSAValidatorService? _validatorService;

        public ECDSAValidatorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ECDSAValidatorService> GetOrDeployValidatorAsync()
        {
            if (_validatorService != null)
                return _validatorService;

            _validatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ECDSAValidatorDeployment());

            return _validatorService;
        }

        [Fact]
        public async Task DeployValidator_Succeeds()
        {
            var service = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ECDSAValidatorDeployment());

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
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
        public async Task IsModuleType_Fallback_ReturnsFalse()
        {
            var validator = await GetOrDeployValidatorAsync();

            var isFallback = await validator.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_FALLBACK);

            Assert.False(isFallback);
        }

        [Fact]
        public async Task IsModuleType_Hook_ReturnsFalse()
        {
            var validator = await GetOrDeployValidatorAsync();

            var isHook = await validator.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_HOOK);

            Assert.False(isHook);
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
        public async Task GetOwner_UnknownAccount_ReturnsZeroAddress()
        {
            var validator = await GetOrDeployValidatorAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var owner = await validator.GetOwnerQueryAsync(unknownAccount);

            Assert.Equal("0x0000000000000000000000000000000000000000", owner.ToLower());
        }
    }
}
