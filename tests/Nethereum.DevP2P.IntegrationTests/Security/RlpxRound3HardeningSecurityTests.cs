using System;
using System.IO;
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
    public class RlpxRound3HardeningSecurityTests
    {
        private static async Task<(RlpxListener listener, RawRlpxPeer attacker, RlpxConnection server)> EstablishAsync()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000, FrameWriteTimeoutMs = 60000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
            await attacker.ReadFrameAsync();
            var server = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return (listener, attacker, server);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_TheDispatcherLoopHasEnded_When_ARequestIsIssued_Then_ItFailsFastInsteadOfWaitingForItsTimeout()
        {
            var serverKey = EthECKey.GenerateKey();
            var serverConfig = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, serverConfig);
            listener.Start(0, IPAddress.Loopback);
            var clientConfig = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000, ReadTimeoutMs = 500 };
            using var client = new RlpxConnection(EthECKey.GenerateKey(), clientConfig);
            try
            {
                await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                client.StartDispatching();

                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!client.IsDisconnected && DateTime.UtcNow < deadline)
                    await Task.Delay(25);
                Assert.True(client.IsDisconnected);

                var started = DateTime.UtcNow;
                var request = client.SendRequestAsync(0x13, new byte[] { 0xc0 }, 0x14, 77, TimeSpan.FromSeconds(30));
                var winner = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(5)));

                Assert.Same(request, winner);
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => request);
                Assert.True(ex is IOException || ex is ObjectDisposedException, ex.GetType().Name);
                Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AConnectionDisposedWhileAWriterHoldsTheWriteLock_When_TheWriterUnwinds_Then_TheDisposedLockDoesNotReplaceTheWriteFailure()
        {
            var (listener, attacker, server) = await EstablishAsync();
            try
            {
                var payload = new byte[15 * 1024 * 1024];
                var completed = 0;
                var writer = Task.Run(async () =>
                {
                    while (true)
                    {
                        await server.SendMessageAsync(0x10, payload);
                        Interlocked.Increment(ref completed);
                    }
                });

                var seen = -1;
                var stalledSince = DateTime.UtcNow;
                var limit = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < limit)
                {
                    await Task.Delay(100);
                    var now = Volatile.Read(ref completed);
                    if (now != seen) { seen = now; stalledSince = DateTime.UtcNow; }
                    else if (DateTime.UtcNow - stalledSince > TimeSpan.FromSeconds(1)) break;
                }
                Assert.False(writer.IsCompleted);

                server.Dispose();

                var ex = await Assert.ThrowsAnyAsync<Exception>(() => writer.WaitAsync(TimeSpan.FromSeconds(10)));
                var disposed = ex as ObjectDisposedException;
                Assert.False(disposed != null && disposed.ObjectName == typeof(System.Threading.SemaphoreSlim).FullName,
                    "the SemaphoreSlim disposed-object error replaced the write failure");
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
