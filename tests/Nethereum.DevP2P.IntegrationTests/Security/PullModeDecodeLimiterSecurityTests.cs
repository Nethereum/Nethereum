using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class PullModeDecodeLimiterSecurityTests
    {
        private const int Connections = 8;
        private const int MessageBytes = 10 * 1024 * 1024 - 4096;
        private const long LimiterCapacity = 12L * 1024 * 1024;

        private sealed class NullHandler : IEth68RequestHandler
        {
            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockBody>>(new List<BlockBody>());
            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<List<Receipt>>());
            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
                => Task.FromResult(new Receipts70Result(new List<List<Receipt>>(), false));
            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
            public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<byte[]>());
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_EightPullModeConnectionsEachDeliveringATenMebibyteMessage_When_TheyAreServed_Then_ConcurrentDecodeBytesStayWithinTheLimiterAndNoConnectionIsDropped()
        {
            var peak = await PeakConcurrentMessageBytesAsync(LimiterCapacity);

            Assert.True(peak > 0);
            Assert.True(peak <= LimiterCapacity, $"{peak} bytes were decoded concurrently, limiter allows {LimiterCapacity}");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ALimiterLargerThanAllTheMessages_When_EightTenMebibyteMessagesAreServedInPullMode_Then_SeveralAreDecodedAtOnce()
        {
            var peak = await PeakConcurrentMessageBytesAsync(Connections * 16L * 1024 * 1024);

            Assert.True(peak > LimiterCapacity, $"only {peak} bytes were decoded concurrently");
        }

        private static byte[] NewBlockHashesOfAboutTenMebibytes()
        {
            var entries = new List<byte[]>();
            var size = 0;
            var hash = new byte[32];
            while (size < MessageBytes - 64)
            {
                var entry = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(hash), RLP.RLP.EncodeElement(new byte[] { 0x07 }));
                entries.Add(entry);
                size += entry.Length;
            }
            return RLP.RLP.EncodeList(entries.ToArray());
        }

        private static async Task<long> PeakConcurrentMessageBytesAsync(long capacity)
        {
            var limiter = new InFlightDecodeLimiter(capacity);
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000, DecodeLimiter = limiter };
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, config);
            var accepted = new System.Collections.Concurrent.ConcurrentDictionary<string, RlpxConnection>();
            var acceptedCount = new SemaphoreSlim(0);
            listener.PeerAccepted += (_, c) => { accepted[c.RemoteEndpoint] = c; acceptedCount.Release(); };
            listener.Start(0, IPAddress.Loopback);

            var attackers = new List<RawRlpxPeer>();
            using var cts = new CancellationTokenSource();
            var loops = new List<Task>();
            try
            {
                for (var i = 0; i < Connections; i++)
                {
                    var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                    await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                    await attacker.ReadFrameAsync();
                    attackers.Add(attacker);
                    Assert.True(await acceptedCount.WaitAsync(TimeSpan.FromSeconds(5)));
                }

                var connections = accepted.Values.ToList();
                Assert.Equal(Connections, connections.Count);
                var message = NewBlockHashesOfAboutTenMebibytes();
                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                };

                long concurrent = 0;
                long peak = 0;
                var handled = 0;
                foreach (var connection in connections)
                {
                    var ethOffset = connection.GetCapabilityOffset("eth");
                    var session = new Eth68ServerSession(connection, new NullHandler(), status);
                    session.BindCapabilityOffset(ethOffset);
                    session.BindRemoteStatus(status);
                    session.NewBlockHashesReceived += (_, _) =>
                    {
                        var now = Interlocked.Add(ref concurrent, message.Length);
                        long observed;
                        while ((observed = Interlocked.Read(ref peak)) < now
                               && Interlocked.CompareExchange(ref peak, now, observed) != observed) { }
                        Thread.Sleep(300);
                        Interlocked.Add(ref concurrent, -message.Length);
                        Interlocked.Increment(ref handled);
                    };
                    loops.Add(Task.Run(async () =>
                    {
                        try { await session.RunAsync(cancellationToken: cts.Token); }
                        catch (OperationCanceledException) { }
                    }));
                }

                var sends = attackers.Select(attacker => Task.Run(async () =>
                {
                    var ethOffset = connections.First(c => c.RemoteNodeId.SequenceEqual(attacker.Key.GetPubKeyNoPrefix()))
                        .GetCapabilityOffset("eth");
                    await attacker.SendFrameAsync(ethOffset + EthMessageIds.NewBlockHashes, message);
                })).ToList();
                await Task.WhenAll(sends);

                var deadline = DateTime.UtcNow.AddSeconds(120);
                while (Volatile.Read(ref handled) < Connections && DateTime.UtcNow < deadline)
                    await Task.Delay(50);

                Assert.Equal(Connections, handled);
                Assert.All(connections, c => Assert.False(c.IsDisconnected));
                Assert.Equal(0, limiter.InFlightBytes);
                return peak;
            }
            finally
            {
                cts.Cancel();
                try { await Task.WhenAll(loops).WaitAsync(TimeSpan.FromSeconds(10)); } catch { }
                foreach (var attacker in attackers) attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
