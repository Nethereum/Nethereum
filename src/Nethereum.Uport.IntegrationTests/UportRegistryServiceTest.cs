using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.TestRPCRunner;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Uport.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class UportRegistryServiceTest
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public UportRegistryServiceTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDeployAContractWithConstructor()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var addressFrom = (await web3.Eth.Accounts.SendRequestAsync())[0];

                    var transactionService = new TransactionReceiptPollingService(web3.TransactionManager);
                    var previousVersionAddress = "0x12890d2cce102216644c59dae5baed380d84830c";
                    var registrySevice = await UportRegistryService.DeployContractAndGetServiceAsync(transactionService,
                        web3,
                        addressFrom,
                        previousVersionAddress,
                        new HexBigInteger(4712388));

                    Assert.Equal(previousVersionAddress, await registrySevice.PreviousPublishedVersionAsyncCall());
        }

        [Fact]
        public async void ShouldRegisterVerification()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var addressFrom = (await web3.Eth.Accounts.SendRequestAsync())[0];

            var transactionService = new TransactionReceiptPollingService(web3.TransactionManager);
            var previousVersionAddress = "0x12890d2cce102216644c59dae5baed380d84830c";
            var registrySevice = await UportRegistryService.DeployContractAndGetServiceAsync(transactionService,
                web3,
                addressFrom,
                previousVersionAddress,
                new HexBigInteger(4712388));

            var attestationId = "superAttestation";
            var subject = "0x22890d2cce102216644c59dae5baed380d84830c";
            var value = "true";

            var txnReceipt = await registrySevice.SetAsyncAndGetReceipt(addressFrom, attestationId, subject,
                value, transactionService, new HexBigInteger(4712388));

            var storedValue = await registrySevice.GetAsyncCall(attestationId, addressFrom, subject);
            Assert.Equal(value, storedValue);
                
        }
    }
}