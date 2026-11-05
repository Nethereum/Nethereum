using System.Numerics;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.AppChain.Anchoring.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.Anchoring.IntegrationTests.SecurityAudit71.S6
{
    [Collection("Hub")]
    public class HubMessageConfirmationTests
    {
        private const ulong TargetChain = 997706;
        private readonly HubFixture _fixture;
        private readonly ITestOutputHelper _out;

        public HubMessageConfirmationTests(HubFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _out = output;
        }

        [Fact]
        [Trait("Finding", "S6-K1")]
        public async Task Given_AHubMessageInTheLatestParentBlock_When_TheLogProcessorRuns_Then_ItIsIndexedOnlyAfterTwelveConfirmations()
        {
            var signature = _fixture.SignRegistration(TargetChain, HubFixture.OwnerAddress);
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(new RegisterAppChainFunction
            {
                ChainId = TargetChain,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            });
            await _fixture.OwnerHubService.SetAuthorizedSenderRequestAndWaitForReceiptAsync(
                TargetChain, HubFixture.SenderAddress, true);
            await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(new SendMessageFunction
            {
                SourceChainId = 1,
                TargetChainId = TargetChain,
                Target = "0x6666666666666666666666666666666666666666",
                Data = new byte[] { 1 },
                AmountToSend = HubFixture.MessageFee
            });

            var store = new InMemoryMessageIndexStore();
            var processor = new HubMessageLogProcessor(store, 1, TargetChain)
            {
                RpcUrl = _fixture.Url,
                HubContractAddress = _fixture.HubContractAddress,
                MinimumBlockConfirmations = 12
            };
            using var cts = new CancellationTokenSource();
            var run = Task.Run(() => processor.ExecuteAsync(cts.Token));

            await Task.Delay(TimeSpan.FromSeconds(4));
            var indexedEarly = await store.GetAsync(1, 1) != null;

            for (var i = 0; i < 14; i++)
                await _fixture.OwnerWeb3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(HubFixture.SenderAddress, 0.0001m);

            var deadline = DateTime.UtcNow.AddSeconds(40);
            var indexedLate = false;
            while (DateTime.UtcNow < deadline && !indexedLate)
            {
                indexedLate = await store.GetAsync(1, 1) != null;
                if (!indexedLate) await Task.Delay(500);
            }
            cts.Cancel();
            try { await run; } catch (OperationCanceledException) { }

            _out.WriteLine($"S6 indexedEarly={indexedEarly} indexedLate={indexedLate}");
            Assert.False(indexedEarly);
            Assert.True(indexedLate);
        }
    }
}
