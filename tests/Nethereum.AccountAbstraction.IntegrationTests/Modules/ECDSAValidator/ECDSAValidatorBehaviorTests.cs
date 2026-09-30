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
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var isInstalled = await account.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR,
                _fixture.ECDSAValidatorService.ContractAddress,
                Array.Empty<byte>());

            Assert.True(isInstalled);
        }

        [Fact]
        public async Task Given_InstalledValidator_When_QueryingOwner_Then_ReturnsCorrectOwner()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var owner = await _fixture.ECDSAValidatorService.GetOwnerQueryAsync(account.ContractAddress);

            Assert.Equal(_fixture.OwnerAddress.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task Given_InstalledValidator_When_CheckingIsInitialized_Then_ReturnsTrue()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var isInitialized = await _fixture.ECDSAValidatorService.IsInitializedQueryAsync(
                account.ContractAddress);

            Assert.True(isInitialized);
        }

        [Fact]
        public async Task Given_ECDSAValidator_When_CheckingModuleType_Then_ReturnsValidatorType()
        {

            var isValidator = await _fixture.ECDSAValidatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_ECDSAValidator_When_CheckingNonMatchingModuleType_Then_ReturnsFalse()
        {

            var isExecutor = await _fixture.ECDSAValidatorService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.False(isExecutor);
        }

        [Fact]
        public async Task Given_InstalledValidator_When_GettingValidatorsPaginated_Then_ValidatorIsListed()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var result = await account.GetValidatorsPaginatedQueryAsync(
                "0x0000000000000000000000000000000000000001",
                10);

            Assert.NotNull(result);
            Assert.NotNull(result.Validators);
            Assert.Contains(result.Validators, v =>
                v.ToLower() == _fixture.ECDSAValidatorService.ContractAddress.ToLower());
        }

        [Fact]
        public void Given_ECDSAValidatorConfig_When_CreatingInitData_Then_ReturnsOwnerAddress()
        {
            var ownerAddress = "0x1234567890123456789012345678901234567890";
            var config = new ECDSAValidatorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Owner = ownerAddress
            };

            var initData = config.GetInitData();

            Assert.Equal(20, initData.Length);
            Assert.Equal(ownerAddress.ToLower(), ("0x" + initData.ToHex()).ToLower());
        }

        [Fact]
        public void Given_ECDSAValidatorConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var ownerAddress = "0x1234567890123456789012345678901234567890";

            var config = ECDSAValidatorConfig.Create(moduleAddress, ownerAddress);

            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(ownerAddress, config.Owner);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_ECDSAValidatorConfigWithNoOwner_When_GettingInitData_Then_Throws()
        {
            var config = new ECDSAValidatorConfig
            {
                ModuleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            };

            Assert.Throws<InvalidOperationException>(() => config.GetInitData());
        }

        [Fact]
        public async Task Given_Account_When_CheckingSupportsValidatorModule_Then_ReturnsTrue()
        {
            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var supportsValidator = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(supportsValidator);
        }
    }
}
