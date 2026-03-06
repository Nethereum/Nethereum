using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Collection("Hub")]
    public class MessagingTests
    {
        private readonly HubFixture _fixture;

        public MessagingTests(HubFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ulong> RegisterWithAuthorizedSenderAsync(ulong chainId)
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

            return chainId;
        }

        [Fact]
        public async Task SendMessage_AuthorizedSender_Succeeds()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992001);

            var sendFunction = new SendMessageFunction
            {
                SourceChainId = 1,
                TargetChainId = chainId,
                Target = "0x1111111111111111111111111111111111111111",
                Data = new byte[] { 0x01, 0x02, 0x03 },
                AmountToSend = HubFixture.MessageFee
            };

            var receipt = await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);

            Assert.NotNull(receipt);
            Assert.NotNull(receipt.TransactionHash);
        }

        [Fact]
        public async Task SendMessage_UnauthorizedSender_Reverts()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992002);

            var sendFunction = new SendMessageFunction
            {
                SourceChainId = 1,
                TargetChainId = chainId,
                Target = "0x1111111111111111111111111111111111111111",
                Data = new byte[] { 0x01 },
                AmountToSend = HubFixture.MessageFee
            };

            await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.OwnerHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction));
        }

        [Fact]
        public async Task GetMessage_ReturnsCorrectData()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992003);

            var messageData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var targetAddress = "0x2222222222222222222222222222222222222222";

            var sendFunction = new SendMessageFunction
            {
                SourceChainId = 1,
                TargetChainId = chainId,
                Target = targetAddress,
                Data = messageData,
                AmountToSend = HubFixture.MessageFee
            };
            await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);

            var message = await _fixture.OwnerHubService.GetMessageQueryAsync(chainId, 1);

            Assert.Equal((ulong)1, message.SourceChainId);
            Assert.Equal(HubFixture.SenderAddress.ToLowerInvariant(), message.Sender.ToLowerInvariant());
            Assert.Equal(targetAddress.ToLowerInvariant(), message.Target.ToLowerInvariant());
            Assert.Equal(messageData, message.Data);
            Assert.True(message.Timestamp > 0);
        }

        [Fact]
        public async Task GetMessageRange_ReturnsBatch()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992004);
            var target = "0x3333333333333333333333333333333333333333";

            for (int i = 0; i < 3; i++)
            {
                var sendFunction = new SendMessageFunction
                {
                    SourceChainId = 1,
                    TargetChainId = chainId,
                    Target = target,
                    Data = new byte[] { (byte)(i + 1) },
                    AmountToSend = HubFixture.MessageFee
                };
                await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);
            }

            var range = await _fixture.OwnerHubService.GetMessageRangeQueryAsync(chainId, 1, 4);

            Assert.NotNull(range.ReturnValue1);
            Assert.Equal(3, range.ReturnValue1.Count);
        }

        [Fact]
        public async Task PendingMessageCount_ReflectsUnprocessed()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992005);
            var target = "0x4444444444444444444444444444444444444444";

            for (int i = 0; i < 2; i++)
            {
                var sendFunction = new SendMessageFunction
                {
                    SourceChainId = 1,
                    TargetChainId = chainId,
                    Target = target,
                    Data = new byte[] { 0x01 },
                    AmountToSend = HubFixture.MessageFee
                };
                await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);
            }

            var pending = await _fixture.OwnerHubService.PendingMessageCountQueryAsync(chainId);
            Assert.Equal((ulong)2, pending);
        }

        [Fact]
        public async Task Anchor_WithProcessedMessageId_UpdatesCount()
        {
            var chainId = await RegisterWithAuthorizedSenderAsync(992006);
            var target = "0x5555555555555555555555555555555555555555";

            for (int i = 0; i < 3; i++)
            {
                var sendFunction = new SendMessageFunction
                {
                    SourceChainId = 1,
                    TargetChainId = chainId,
                    Target = target,
                    Data = new byte[] { 0x01 },
                    AmountToSend = HubFixture.MessageFee
                };
                await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);
            }

            var roots = new byte[32];
            roots[0] = 0x01;
            await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 100, roots, roots, roots, 2, Array.Empty<byte>());

            var pending = await _fixture.OwnerHubService.PendingMessageCountQueryAsync(chainId);
            Assert.Equal((ulong)1, pending);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);
            Assert.Equal((ulong)2, info.LastProcessedMessageId);
        }
    }
}
