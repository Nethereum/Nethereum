using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Collection("Hub")]
    public class RegistrationTests
    {
        private readonly HubFixture _fixture;

        public RegistrationTests(HubFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RegisterAppChain_WithValidSignature_Succeeds()
        {
            var chainId = (ulong)990001;
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            var receipt = await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);

            Assert.NotNull(receipt);
            Assert.NotNull(receipt.TransactionHash);
        }

        [Fact]
        public async Task RegisterAppChain_DuplicateChainId_Reverts()
        {
            var chainId = (ulong)990002;
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);

            var secondSignature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);
            var duplicateFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = secondSignature,
                AmountToSend = HubFixture.RegistrationFee
            };

            await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(duplicateFunction));
        }

        [Fact]
        public async Task GetAppChainInfo_ReturnsCorrectData()
        {
            var chainId = (ulong)990003;
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);

            Assert.True(info.Registered);
            Assert.Equal(HubFixture.OwnerAddress.ToLowerInvariant(), info.Owner.ToLowerInvariant());
            Assert.Equal(HubFixture.SequencerAddress.ToLowerInvariant(), info.Sequencer.ToLowerInvariant());
            Assert.Equal((ulong)0, info.LatestBlock);
            Assert.Equal((ulong)1, info.NextMessageId);
        }

        [Fact]
        public async Task SetSequencer_AsOwner_Succeeds()
        {
            var chainId = (ulong)990004;
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);

            var newSequencer = HubFixture.SenderAddress;
            await _fixture.OwnerHubService.SetSequencerRequestAndWaitForReceiptAsync(chainId, newSequencer);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);
            Assert.Equal(newSequencer.ToLowerInvariant(), info.Sequencer.ToLowerInvariant());
        }

        [Fact]
        public async Task SetAuthorizedSender_AsOwner_Succeeds()
        {
            var chainId = (ulong)990005;
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);

            await _fixture.OwnerHubService.SetAuthorizedSenderRequestAndWaitForReceiptAsync(
                chainId, HubFixture.SenderAddress, true);

            var isAuthorized = await _fixture.OwnerHubService.AuthorizedSendersQueryAsync(
                chainId, HubFixture.SenderAddress);
            Assert.True(isAuthorized);
        }

        [Fact]
        public async Task HubOwner_ReturnsDeployer()
        {
            var owner = await _fixture.OwnerHubService.HubOwnerQueryAsync();
            Assert.Equal(HubFixture.OwnerAddress.ToLowerInvariant(), owner.ToLowerInvariant());
        }
    }
}
