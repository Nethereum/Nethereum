using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Documentation;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxLimitsDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "InFlightDecodeLimiter — bound the bytes decoded at once")]
        public async Task Given_ALimiterSizedForMaxPeers_When_PayloadsAreLeased_Then_InFlightBytesAreCountedUntilTheLeaseIsDisposed()
        {
            var limiter = InFlightDecodeLimiter.ForMaxPeers(25);
            var config = DevP2PConfig.ForDevChain(new byte[32]);
            config.DecodeLimiter = limiter;

            var payload = new byte[1024 * 1024];
            using (var lease = await limiter.AcquireAsync(payload.Length))
            {
                Assert.Equal(payload.Length, limiter.InFlightBytes);
            }

            Assert.Equal(0, limiter.InFlightBytes);
            Assert.Equal(25 * InFlightDecodeLimiter.BytesPerPeer, limiter.CapacityBytes);
            Assert.Equal(limiter.CapacityBytes, InFlightDecodeLimiter.Shared.CapacityBytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxPeerSlots — claim one slot per admitted peer")]
        public void Given_ThreeSlots_When_PeersClaimThem_Then_OnlyTrustedPeersExceedTheLimit()
        {
            var slots = new RlpxPeerSlots(maxPeers: 3);

            Assert.True(slots.TryAcquire(trusted: false));
            Assert.True(slots.TryAcquire(trusted: false));
            Assert.True(slots.TryAcquire(trusted: false));
            Assert.False(slots.TryAcquire(trusted: false));
            Assert.True(slots.TryAcquire(trusted: true));
            Assert.Equal(4, slots.Active);

            slots.Release();
            Assert.Equal(3, slots.Active);
            Assert.False(slots.TryAcquire(trusted: false));
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxResponseBound — cap what a response may hold")]
        public void Given_ResponseBounds_When_Built_Then_TheyCarryTheirKindAndLimit()
        {
            var items = RlpxResponseBound.Items(1024);
            var bytes = RlpxResponseBound.ContentBytes(2 * 1024 * 1024);

            Assert.Equal(RlpxResponseBoundKind.Items, items.Kind);
            Assert.Equal(1024, items.Limit);
            Assert.Equal(RlpxResponseBoundKind.ContentBytes, bytes.Kind);
            Assert.Equal(2 * 1024 * 1024, bytes.Limit);
            Assert.Equal(RlpxResponseBoundKind.Unbounded, RlpxResponseBound.Unbounded.Kind);
            Assert.Equal(RlpxResponseBoundKind.Unbounded, RlpxResponseBound.ContentBytes(0).Kind);

            Assert.True(RlpxResponseCodes.IsResponse("eth", 0x04));
            Assert.False(RlpxResponseCodes.IsResponse("eth", 0x03));
            Assert.False(RlpxResponseCodes.IsResponse("les", 0x04));
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "DevP2PConfig.IsLanAddress — addresses exempt from the per-IP inbound cap")]
        public void Given_TheDefaultConfig_When_AddressesAreClassified_Then_PrivateRangesAreLanAndPublicOnesAreNot()
        {
            var config = DevP2PConfig.ForDevChain(new byte[32]);

            Assert.True(config.IsLanAddress(IPAddress.Loopback));
            Assert.True(config.IsLanAddress(IPAddress.Parse("10.1.2.3")));
            Assert.False(config.IsLanAddress(IPAddress.Parse("8.8.8.8")));

            config.IsLanAddress = _ => false;
            Assert.False(config.IsLanAddress(IPAddress.Loopback));
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "DevP2PConfig.WithMaxEthVersion — cap the advertised eth version")]
        public async Task Given_AClientCappedAtEth69_When_ItDialsAListener_Then_BothSidesShareEth69()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = DevP2PConfig.ForDevChain(new byte[32]);
            var capped = config.WithMaxEthVersion(69);

            Assert.Equal(71, config.MaxEthVersion);
            Assert.Equal(69, capped.MaxEthVersion);

            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var client = new RlpxConnection(clientKey, capped);
            try
            {
                using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
                await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix(), cts.Token);
                var serverSide = await accepted.Task.WaitAsync(cts.Token);

                Assert.Equal(69, client.SharedCapabilities.Find(c => c.Name == "eth").Version);
                Assert.Equal(69, serverSide.SharedCapabilities.Find(c => c.Name == "eth").Version);
            }
            finally
            {
                await client.DisconnectAsync();
                await listener.StopAsync();
            }
        }
    }
}
