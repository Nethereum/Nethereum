using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Documentation;
using Nethereum.Util.Rest;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests.SecurityAudit71.S8
{
    public class LightClientSecurityAuditTests
    {
        private const ulong Mid = S8Support.PeriodStart + 4000;

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "A single signer cannot replace the optimistic header", Order = 22)]
        public async Task S8_01_Given_HonestHeadSignedBy342_When_UpdateSignedByOneMemberArrives_Then_OptimisticHeaderIsNotReplaced()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(Mid, 100);
            var honest = S8Support.PlainUpdate(Mid + 10, 110, participants: 342);
            var forged = S8Support.PlainUpdate(Mid + 20, 120, participants: 1);
            var (service, _) = await S8Support.StartAsync(bootstrap, honest, forged);

            await service.UpdateAsync();

            Assert.Equal(Mid + 10, service.GetState().OptimisticSlot);
        }

        [Fact]
        public async Task S8_02_Given_StoreWithUnknownNextCommittee_When_OlderGenuineCommitteeUpdateArrives_Then_FinalizedHeaderDoesNotGoBackwards()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(Mid, 100);
            var older = S8Support.CommitteeUpdate(
                attestedSlot: Mid + 10, attestedBlock: 110,
                finalizedSlot: Mid - 100, finalizedBlock: 90,
                next: S8Support.Committee(0x55), participants: 342, withFinality: true);
            var (service, _) = await S8Support.StartAsync(bootstrap, older);

            var applied = await service.UpdateAsync();

            Assert.True(applied, service.LastRejectReason);
            Assert.True(LightClientService.IsNextSyncCommitteeKnown(service.GetState()));
            Assert.Equal(Mid, service.GetState().FinalizedSlot);
        }

        [Fact]
        public async Task S8_03_Given_StoreFinalizedAtSlotX_When_CommitteeUpdateAttestedBeforeXArrives_Then_OptimisticIsNotOlderThanFinalized()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(Mid, 100);
            var older = S8Support.CommitteeUpdate(
                attestedSlot: Mid - 50, attestedBlock: 60,
                finalizedSlot: Mid - 100, finalizedBlock: 50,
                next: S8Support.Committee(0x55), participants: 342, withFinality: false);
            var (service, _) = await S8Support.StartAsync(bootstrap, older);

            await service.UpdateAsync();

            var provider = new TrustedHeaderProvider(service);
            Assert.True(provider.GetLatestOptimistic().BlockNumber >= provider.GetLatestFinalized().BlockNumber);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "A header is stale by its own execution timestamp", Order = 20)]
        public async Task S8_04_Given_BootstrapHeaderFarInThePast_When_FinalizedHeaderRequestedWithThrowOnStale_Then_StaleHeaderExceptionRaised()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(Mid, 100);
            var (service, _) = await S8Support.StartAsync(bootstrap);
            var provider = new TrustedHeaderProvider(service) { ThrowOnStaleHeader = true };

            Assert.Throws<StaleHeaderException>(() => provider.GetLatestFinalized());
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "A replayed old head reports stale", Order = 21)]
        public async Task S8_05_Given_ReplayedOldGenuineHead_When_AppliedNow_Then_OptimisticHeaderReportsStale()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(Mid, 100);
            var replay = S8Support.PlainUpdate(Mid + 10, 110, participants: 342);
            var (service, _) = await S8Support.StartAsync(bootstrap, replay);
            Assert.True(await service.UpdateAsync(), service.LastRejectReason);
            var provider = new TrustedHeaderProvider(service) { ThrowOnStaleHeader = true };

            Assert.Throws<StaleHeaderException>(() => provider.GetLatestOptimistic());
        }

        [Fact]
        public async Task S8_06_Given_EndpointServing48MiBBody_When_GetUpdatesAsync_Then_ClientRefusesTheOversizedResponse()
        {
            var body = "[" + new string(' ', 48 * 1024 * 1024) + "]";
            var client = new LightClientApiClient("http://beacon.invalid", new RestHttpHelper(new HttpClient(new FixedBodyHandler(body))));

            await Assert.ThrowsAnyAsync<Exception>(() => client.GetUpdatesAsync(0, 4));
        }

        private sealed class FixedBodyHandler : HttpMessageHandler
        {
            private readonly string _body;
            public FixedBodyHandler(string body) { _body = body; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
