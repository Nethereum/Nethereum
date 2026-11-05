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
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxDispatcherNestedRlpCrashSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AuthenticatedPeerSendingDeeplyNestedRlpInAnUnsolicitedFrame_When_DispatcherInspectsTheRequestId_Then_ProcessSurvivesAndTheConnectionStillAnswersAPing()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await Security.RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(Security.RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                serverConn.StartDispatching();

                var payload = new byte[1024 * 1024];
                Array.Fill(payload, (byte)0xc1);
                await attacker.SendFrameAsync(0x11, payload);

                await attacker.SendFrameAsync(0x02, new byte[] { 0xc0 });
                var (msgId, _) = await attacker.ReadFrameAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

                Assert.Equal(0x03, msgId);
                Assert.False(serverConn.IsDisconnected);
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
