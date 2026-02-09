using Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Paymasters
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class TokenPaymasterTests
    {
        private readonly BundlerTestFixture _fixture;
        private TokenPaymasterService? _paymasterService;

        public TokenPaymasterTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<TokenPaymasterService> GetOrDeployPaymasterAsync()
        {
            if (_paymasterService != null)
                return _paymasterService;

            var deployment = new TokenPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            _paymasterService = await TokenPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            return _paymasterService;
        }

        [Fact]
        public async Task DeployPaymaster_WithValidParams_Succeeds()
        {
            var deployment = new TokenPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            var service = await TokenPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var entryPoint = await paymaster.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetOwner_ReturnsConfiguredOwner()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal(
                _fixture.BeneficiaryAddress.ToLower(),
                owner.ToLower());
        }

        [Fact]
        public async Task GetDeposit_ReturnsZeroForNewPaymaster()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var deposit = await paymaster.GetDepositQueryAsync();

            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task Deposit_IncreasesDeposit()
        {
            var paymaster = await GetOrDeployPaymasterAsync();
            var depositAmount = Web3.Web3.Convert.ToWei(0.01m);

            var initialDeposit = await paymaster.GetDepositQueryAsync();

            var depositFunction = new DepositFunction
            {
                AmountToSend = depositAmount
            };
            await paymaster.DepositRequestAndWaitForReceiptAsync(depositFunction);

            var newDeposit = await paymaster.GetDepositQueryAsync();

            Assert.Equal(initialDeposit + depositAmount, newDeposit);
        }

        [Fact]
        public async Task GetToken_ReturnsZeroAddressWhenNotSet()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var token = await paymaster.TokenQueryAsync();

            Assert.Equal("0x0000000000000000000000000000000000000000", token.ToLower());
        }

        [Fact]
        public async Task GetPriceMarkup_ReturnsDefaultValue()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var markup = await paymaster.PriceMarkupQueryAsync();

            Assert.True(markup >= 0);
        }

        [Fact]
        public async Task GetMarkupDenominator_ReturnsValue()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var denominator = await paymaster.MarkupDenominatorQueryAsync();

            Assert.True(denominator > 0);
        }

        [Fact]
        public async Task GetPriceDecimals_ReturnsValue()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var decimals = await paymaster.PriceDecimalsQueryAsync();

            Assert.True(decimals >= 0);
        }

        [Fact]
        public async Task GetPriceOracle_ReturnsZeroAddressWhenNotSet()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var oracle = await paymaster.PriceOracleQueryAsync();

            Assert.Equal("0x0000000000000000000000000000000000000000", oracle.ToLower());
        }
    }
}
