using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxDecodeLimiterSecurityTests
    {
        private const int Connections = 8;
        private const int MessageBytes = 10 * 1024 * 1024 - 1024;
        private const long LimiterCapacity = 12L * 1024 * 1024;

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_EightConnectionsEachDeliveringATenMebibyteResponse_When_TheyAreDecoded_Then_ConcurrentDecodeBytesStayWithinTheLimiterAndNoConnectionIsDropped()
        {
            var peak = await PeakConcurrentDecodeBytesAsync(LimiterCapacity);

            Assert.True(peak > 0);
            Assert.True(peak <= LimiterCapacity, $"{peak} bytes were decoded concurrently, limiter allows {LimiterCapacity}");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ALimiterLargerThanAllTheMessages_When_EightTenMebibyteResponsesAreDecoded_Then_SeveralAreDecodedAtOnce()
        {
            var peak = await PeakConcurrentDecodeBytesAsync(Connections * 16L * 1024 * 1024);

            Assert.True(peak > LimiterCapacity, $"only {peak} bytes were decoded concurrently");
        }

        private static async Task<long> PeakConcurrentDecodeBytesAsync(long capacity)
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
                var content = new byte[MessageBytes];
                Array.Fill(content, (byte)0x01);
                var itemList = RLP.RLP.EncodeList(content);

                long concurrent = 0;
                long peak = 0;
                var requests = new List<Task<int>>();
                var responses = new List<Task>();
                for (var i = 0; i < Connections; i++)
                {
                    var connection = connections[i];
                    connection.StartDispatching();
                    var ethOffset = connection.GetCapabilityOffset("eth");
                    var requestId = connection.NextRequestId();
                    requests.Add(connection.SendRequestAsync(
                        ethOffset + EthMessageIds.GetBlockHeaders,
                        GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage { RequestId = requestId, StartBlock = 1, Limit = MessageBytes }),
                        ethOffset + EthMessageIds.BlockHeaders, requestId, TimeSpan.FromSeconds(60),
                        RlpxResponseBound.Items(MessageBytes),
                        payload =>
                        {
                            var now = Interlocked.Add(ref concurrent, payload.Length);
                            long observed;
                            while ((observed = Interlocked.Read(ref peak)) < now
                                   && Interlocked.CompareExchange(ref peak, now, observed) != observed) { }
                            Thread.Sleep(300);
                            Interlocked.Add(ref concurrent, -payload.Length);
                            return payload.Length;
                        }));

                    var attacker = attackers.First(a => a.Key.GetPubKeyNoPrefix().SequenceEqual(connection.RemoteNodeId));
                    var message = RLP.RLP.EncodeList(
                        RLP.RLP.EncodeElement(((long)requestId).ToBytesForRLPEncoding()),
                        itemList);
                    responses.Add(Task.Run(async () =>
                    {
                        await attacker.ReadFrameAsync();
                        await attacker.SendFrameAsync(ethOffset + EthMessageIds.BlockHeaders, message);
                    }));
                }

                await Task.WhenAll(responses);
                var lengths = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(120));

                Assert.All(lengths, length => Assert.True(length > 0));
                Assert.All(connections, c => Assert.False(c.IsDisconnected));
                Assert.Equal(0, limiter.InFlightBytes);
                return peak;
            }
            finally
            {
                foreach (var attacker in attackers) attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
