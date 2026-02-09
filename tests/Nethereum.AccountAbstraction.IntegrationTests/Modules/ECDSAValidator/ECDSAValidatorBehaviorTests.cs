using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.ECDSAValidator
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "ECDSAValidator")]
    public class ECDSAValidatorBehaviorTests
    {
        private readonly ERC7579TestFixture _fixture;

        public ECDSAValidatorBehaviorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Given_AccountCreatedWithValidator_When_QueryingIsInstalled_Then_ReturnsTrue()
        {
            // Given: A smart account created with ECDSA validator (installed during creation)
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Checking if the validator is installed
            var isInstalled = await account.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR,
                _fixture.ECDSAValidatorService.ContractAddress,
                Array.Empty<byte>());

            // Then: The validator is installed
            Assert.True(isInstalled);
        }

        [Fact]
        public async Task Given_InstalledValidator_When_QueryingOwner_Then_ReturnsCorrectOwner()
        {
            // Given: A smart account with ECDSA validator installed
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Querying the owner from the validator
            var owner = await _fixture.ECDSAValidatorService.GetOwnerQueryAsync(account.ContractAddress);

            // Then: The owner matches the configured owner
            Assert.Equal(_fixture.OwnerAddress.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task Given_InstalledValidator_When_CheckingIsInitialized_Then_ReturnsTrue()
        {
            // Given: A smart account with ECDSA validator
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Checking if validator is initialized for the account
            var isInitialized = await _fixture.ECDSAValidatorService.IsInitializedQueryAsync(
                account.ContractAddress);

            // Then: Validator is initialized
            Assert.True(isInitialized);
        }

        [Fact]
        public async Task Given_ECDSAValidator_When_CheckingModuleType_Then_ReturnsValidatorType()
        {
            // Given: An ECDSA validator contract

            // When: Checking if it's a validator type module
            var isValidator = await _fixture.ECDSAValidatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: It confirms it's a validator
            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_ECDSAValidator_When_CheckingNonMatchingModuleType_Then_ReturnsFalse()
        {
            // Given: An ECDSA validator contract

            // When: Checking if it's an executor type module
            var isExecutor = await _fixture.ECDSAValidatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_EXECUTOR);

            // Then: It's not an executor
            Assert.False(isExecutor);
        }

        [Fact]
        public async Task Given_InstalledValidator_When_GettingValidatorsPaginated_Then_ValidatorIsListed()
        {
            // Given: A smart account with validator installed
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Getting paginated validators
            var result = await account.GetValidatorsPaginatedQueryAsync(
                "0x0000000000000000000000000000000000000001",
                10);

            // Then: The validator is in the list
            Assert.NotNull(result);
            Assert.NotNull(result.Validators);
            Assert.Contains(result.Validators, v =>
                v.ToLower() == _fixture.ECDSAValidatorService.ContractAddress.ToLower());
        }

        [Fact]
        public void Given_ECDSAValidatorConfig_When_CreatingInitData_Then_ReturnsOwnerAddress()
        {
            // Given: A validator config with owner
            var ownerAddress = "0x1234567890123456789012345678901234567890";
            var config = new ECDSAValidatorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Owner = ownerAddress
            };

            // When: Getting init data
            var initData = config.GetInitData();

            // Then: Init data is the owner address (20 bytes)
            Assert.Equal(20, initData.Length);
            Assert.Equal(ownerAddress.ToLower(), ("0x" + initData.ToHex()).ToLower());
        }

        [Fact]
        public void Given_ECDSAValidatorConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            // Given: Config created via static method
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var ownerAddress = "0x1234567890123456789012345678901234567890";

            // When: Using static Create method
            var config = ECDSAValidatorConfig.Create(moduleAddress, ownerAddress);

            // Then: Config is properly set
            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(ownerAddress, config.Owner);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_ECDSAValidatorConfigWithNoOwner_When_GettingInitData_Then_Throws()
        {
            // Given: A config without owner
            var config = new ECDSAValidatorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            };

            // When/Then: Getting init data throws
            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public async Task Given_Account_When_CheckingSupportsValidatorModule_Then_ReturnsTrue()
        {
            // Given: A smart account
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Checking if account supports validator modules
            var supportsValidator = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: Account supports validators
            Assert.True(supportsValidator);
        }
    }
}
