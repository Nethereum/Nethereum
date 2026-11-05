using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxRound3SecurityTests
    {
        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AuthenticatedPeerSendingAMessageIdOutsideEveryNegotiatedCapability_When_TheServerReadsIt_Then_TheConnectionIsDropped()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                serverConn.StartDispatching();

                await attacker.SendFrameAsync(0x7000, new byte[] { 0xc0 });

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!serverConn.IsDisconnected && DateTime.UtcNow < deadline)
                    await Task.Delay(50);

                Assert.True(serverConn.IsDisconnected);
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ReleaseCalledWithoutAMatchingAcquire_When_SlotsAreCounted_Then_ActiveNeverGoesNegativeAndTheCapStillHolds()
        {
            var slots = new RlpxPeerSlots(1);

            slots.Release();
            var first = slots.TryAcquire(false);
            var second = slots.TryAcquire(false);

            Assert.True(slots.Active >= 0);
            Assert.True(first);
            Assert.False(second);
        }
    }
}
