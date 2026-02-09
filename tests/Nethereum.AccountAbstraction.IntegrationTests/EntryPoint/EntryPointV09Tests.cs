using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.XUnitEthereumClients;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.EntryPoint
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EntryPointV09Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EntryPointV09Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task ShouldGetSenderCreatorAddress()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var senderCreator = await entryPointService.SenderCreatorQueryAsync();

            Assert.NotNull(senderCreator);
            Assert.StartsWith("0x", senderCreator);
            Assert.Equal(42, senderCreator.Length);
        }

        [Fact]
        public async Task ShouldGetEip712Domain()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var domain = await entryPointService.Eip712DomainQueryAsync();

            Assert.NotNull(domain);
            Assert.NotNull(domain.Name);
            Assert.NotNull(domain.Version);
            Assert.Equal(entryPointService.ContractAddress.ToLower(), domain.VerifyingContract.ToLower());
        }

        [Fact]
        public async Task ShouldGetDomainSeparator()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var domainSeparator = await entryPointService.GetDomainSeparatorV4QueryAsync();

            Assert.NotNull(domainSeparator);
            Assert.Equal(32, domainSeparator.Length);
        }

        [Fact]
        public async Task ShouldGetPackedUserOpTypeHash()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var typeHash = await entryPointService.GetPackedUserOpTypeHashQueryAsync();

            Assert.NotNull(typeHash);
            Assert.Equal(32, typeHash.Length);
        }

        [Fact]
        public async Task ShouldSupportsInterface()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var iEntryPointInterfaceId = new byte[] { 0x01, 0xff, 0xc9, 0xa7 };
            var supportsEntryPoint = await entryPointService.SupportsInterfaceQueryAsync(iEntryPointInterfaceId);

            Assert.True(supportsEntryPoint);
        }

        [Fact]
        public async Task ShouldGetNonceForNewAccount()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var newAccountAddress = "0x1234567890123456789012345678901234567890";
            var nonce = await entryPointService.GetNonceQueryAsync(newAccountAddress, 0);

            Assert.Equal(0, nonce);
        }

        [Fact]
        public async Task ShouldGetBalanceForUnfundedAccount()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var newAccountAddress = "0x1234567890123456789012345678901234567890";
            var balance = await entryPointService.BalanceOfQueryAsync(newAccountAddress);

            Assert.Equal(0, balance);
        }

        [Fact]
        public async Task ShouldDepositAndGetBalance()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var accountAddress = EthereumClientIntegrationFixture.AccountAddress;
            var depositAmount = Web3.Web3.Convert.ToWei(0.01m);

            var depositFunction = new DepositToFunction
            {
                Account = accountAddress,
                AmountToSend = depositAmount
            };
            await entryPointService.DepositToRequestAndWaitForReceiptAsync(depositFunction);

            var balance = await entryPointService.BalanceOfQueryAsync(accountAddress);

            Assert.Equal(depositAmount, balance);
        }

        [Fact]
        public async Task ShouldGetDepositInfo()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(web3, new EntryPointDeployment());

            var accountAddress = EthereumClientIntegrationFixture.AccountAddress;
            var depositAmount = Web3.Web3.Convert.ToWei(0.01m);

            var depositFunction = new DepositToFunction
            {
                Account = accountAddress,
                AmountToSend = depositAmount
            };
            await entryPointService.DepositToRequestAndWaitForReceiptAsync(depositFunction);

            var depositInfo = await entryPointService.GetDepositInfoQueryAsync(accountAddress);

            Assert.NotNull(depositInfo);
            Assert.Equal(depositAmount, depositInfo.Info.Deposit);
            Assert.False(depositInfo.Info.Staked);
        }
    }
}
