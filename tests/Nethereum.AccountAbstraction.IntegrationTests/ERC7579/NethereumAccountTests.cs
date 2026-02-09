using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.ERC7579
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class NethereumAccountTests
    {
        private readonly ERC7579TestFixture _fixture;

        public NethereumAccountTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<NethereumAccountService> CreateAndFundAccountAsync(ulong saltValue = 0)
        {
            if (saltValue == 0)
                saltValue = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var salt = _fixture.CreateSalt(saltValue);
            var account = await _fixture.CreateAccountAsync(salt);
            await _fixture.FundAccountAsync(account.ContractAddress, 0.1m);

            return account;
        }

        [Fact]
        public async Task GetAccountId_ReturnsValidId()
        {
            var account = await CreateAndFundAccountAsync();

            var accountId = await account.AccountIdQueryAsync();

            Assert.NotNull(accountId);
            Assert.NotEmpty(accountId);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var account = await CreateAndFundAccountAsync();

            var entryPoint = await account.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetDeposit_ReturnsZeroForNewAccount()
        {
            var account = await CreateAndFundAccountAsync();

            var deposit = await account.GetDepositQueryAsync();

            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task AddDeposit_IncreasesDeposit()
        {
            var account = await CreateAndFundAccountAsync();
            var depositAmount = Web3.Web3.Convert.ToWei(0.01m);

            var initialDeposit = await account.GetDepositQueryAsync();

            var addDepositFunction = new Contracts.Core.NethereumAccount.ContractDefinition.AddDepositFunction
            {
                AmountToSend = depositAmount
            };
            await account.AddDepositRequestAndWaitForReceiptAsync(addDepositFunction);

            var newDeposit = await account.GetDepositQueryAsync();

            Assert.Equal(initialDeposit + depositAmount, newDeposit);
        }

        [Fact]
        public async Task GetNonce_ReturnsZeroForNewAccount()
        {
            var account = await CreateAndFundAccountAsync();

            var nonce = await account.GetNonceQueryAsync(0);

            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task SupportsModule_Validator_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();

            var supportsValidator = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(supportsValidator);
        }

        [Fact]
        public async Task SupportsModule_Executor_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();

            var supportsExecutor = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.True(supportsExecutor);
        }

        [Fact]
        public async Task SupportsModule_Fallback_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();

            var supportsFallback = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_FALLBACK);

            Assert.True(supportsFallback);
        }

        [Fact]
        public async Task SupportsModule_Hook_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();

            var supportsHook = await account.SupportsModuleQueryAsync(ERC7579ModuleTypes.TYPE_HOOK);

            Assert.True(supportsHook);
        }

        [Fact]
        public async Task SupportsExecutionMode_SingleDefault_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();
            var mode = ERC7579ModeLib.EncodeSingleDefault();

            var supportsMode = await account.SupportsExecutionModeQueryAsync(mode);

            Assert.True(supportsMode);
        }

        [Fact]
        public async Task SupportsExecutionMode_BatchDefault_ReturnsTrue()
        {
            var account = await CreateAndFundAccountAsync();
            var mode = ERC7579ModeLib.EncodeBatchDefault();

            var supportsMode = await account.SupportsExecutionModeQueryAsync(mode);

            Assert.True(supportsMode);
        }

        [Fact]
        public async Task GetValidatorsPaginated_ReturnsValidators()
        {
            var account = await CreateAndFundAccountAsync();

            var result = await account.GetValidatorsPaginatedQueryAsync(
                "0x0000000000000000000000000000000000000001",
                10);

            Assert.NotNull(result);
            Assert.NotNull(result.Validators);
        }

        [Fact]
        public async Task GetExecutorsPaginated_ReturnsExecutors()
        {
            var account = await CreateAndFundAccountAsync();

            var result = await account.GetExecutorsPaginatedQueryAsync(
                "0x0000000000000000000000000000000000000001",
                10);

            Assert.NotNull(result);
            Assert.NotNull(result.Executors);
        }

        [Fact]
        public async Task GetEmergencyUninstallDelay_ReturnsDelay()
        {
            var account = await CreateAndFundAccountAsync();

            var delay = await account.EmergencyUninstallDelayQueryAsync();

            Assert.True(delay >= 0);
        }

        [Fact]
        public async Task GetUpgradeInterfaceVersion_ReturnsVersion()
        {
            var account = await CreateAndFundAccountAsync();

            var version = await account.UpgradeInterfaceVersionQueryAsync();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

        [Fact]
        public async Task ProxiableUUID_ReturnsValidUUID()
        {
            var account = await CreateAndFundAccountAsync();

            var uuid = await account.ProxiableUUIDQueryAsync();

            Assert.NotNull(uuid);
            Assert.True(uuid.Length > 0);
        }
    }
}
