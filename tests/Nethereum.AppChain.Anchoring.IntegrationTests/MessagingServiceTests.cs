using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.AppChain.Anchoring.Messaging;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Collection("Hub")]
    public class MessagingServiceTests
    {
        private readonly HubFixture _fixture;

        public MessagingServiceTests(HubFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ulong> RegisterWithMessagesAsync(ulong chainId, int messageCount)
        {
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

            for (int i = 0; i < messageCount; i++)
            {
                var sendFunction = new SendMessageFunction
                {
                    SourceChainId = 1,
                    TargetChainId = chainId,
                    Target = "0x6666666666666666666666666666666666666666",
                    Data = new byte[] { (byte)(i + 1) },
                    AmountToSend = HubFixture.MessageFee
                };
                await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);
            }

            return chainId;
        }

        [Fact]
        public async Task HubMessageService_GetPendingMessages_ReturnsAll()
        {
            var chainId = await RegisterWithMessagesAsync(993001, 3);

            var service = new HubMessageService(
                _fixture.OwnerWeb3, _fixture.HubContractAddress, chainId);

            var messages = await service.GetPendingMessagesAsync(0, 100);

            Assert.Equal(3, messages.Count);
            Assert.Equal((ulong)1, messages[0].MessageId);
            Assert.Equal((ulong)2, messages[1].MessageId);
            Assert.Equal((ulong)3, messages[2].MessageId);
        }

        [Fact]
        public async Task HubMessageService_GetPendingMessages_RespectsLastProcessedId()
        {
            var chainId = await RegisterWithMessagesAsync(993002, 5);

            var service = new HubMessageService(
                _fixture.OwnerWeb3, _fixture.HubContractAddress, chainId);

            var messages = await service.GetPendingMessagesAsync(2, 100);

            Assert.Equal(3, messages.Count);
            Assert.Equal((ulong)3, messages[0].MessageId);
        }

        [Fact]
        public async Task MessagingWorker_DisabledConfig_DoesNotStart()
        {
            var config = new MessagingConfig { Enabled = false };
            var messagingService = new MessagingService(420420, config, new InMemoryMessageIndexStore());
            var worker = new MessagingWorker(messagingService, config);

            await worker.StartAsync(CancellationToken.None);

            Assert.False(worker.IsRunning);

            await worker.StopAsync(CancellationToken.None);
        }
    }
}
