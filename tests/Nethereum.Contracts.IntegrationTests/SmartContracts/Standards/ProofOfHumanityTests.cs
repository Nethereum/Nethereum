using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts.Standards
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ProofOfHumanityTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ProofOfHumanityTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldRetrieveAddressRegistered()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var isRegistered = await web3.Eth.ProofOfHumanity.GetContractService().IsRegisteredQueryAsync("0x1db3439a222c519ab44bb1144fc28167b4fa6ee6").ConfigureAwait(false);
            Assert.True(isRegistered);
        }

        [Fact]
        public async void ShouldRetrieveMultipleAddressRegistered()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var registrations = await web3.Eth.ProofOfHumanity.GetContractService().AreRegisteredQueryUsingMulticallAsync(new[] { "0x1db3439a222c519ab44bb1144fc28167b4fa6ee6", "0x2ad91063e489cc4009df7fee45c25c8be684cf6a", "0x2A52309eDF998799C4A8b89324CCAd91848c8676", "0x0000000000000000000000000000000000000000" }).ConfigureAwait(false);
            Assert.True(registrations[0].IsRegistered);
            Assert.False(registrations[1].IsRegistered);
            Assert.False(registrations[2].IsRegistered);
            Assert.False(registrations[3].IsRegistered);
        }

        [Fact]
        public async void ShouldRetrieveEvidenceFromLogs()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var service = web3.Eth.ProofOfHumanity.GetContractService();
            var log = await service.GetLatestEvidenceLogAsync("0x1db3439a222c519ab44bb1144fc28167b4fa6ee6").ConfigureAwait(false);
            Assert.Equal("/ipfs/QmSdWucTAumLeYsJjEJTFduJ63vuKEjnu9dUweuRZc6srD/registration.json", log.Event.Evidence);
            var registrationEvidence = await service.GetRegistrationEvidenceFromIpfs(log.Event, "https://cf-ipfs.com/").ConfigureAwait(false);
            Assert.Equal("Vitalik Buterin", registrationEvidence.Name);
        }

    
        //VITALIK
        //"0x1db3439a222c519ab44bb1144fc28167b4fa6ee6
        //JUANU
        //0x2ad91063e489cc4009df7fee45c25c8be684cf6a
        //SANTI
        //0x2A52309eDF998799C4A8b89324CCAd91848c8676
        //0x0000000000000000000000000000000000000000
    }
}