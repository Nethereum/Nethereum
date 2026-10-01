using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    public class SnapTrieNodesVersionGuardTests
    {
        private const ulong NetworkId = 1337;

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (var i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static DevP2PConfig ServerConfig(bool advertiseSnap2) => new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests/snap-guard-server",
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 2_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000,
            AdvertiseSnap2 = advertiseSnap2
        };

        private static async Task<(SyncPeerSession Client, RlpxListener Listener, RlpxConnection Server)>
            ConnectAsync(bool advertiseSnap2)
        {
            var serverKey = EthECKey.GenerateKey();
            var genesis = Make32(0xC0);

            var listener = new RlpxListener(serverKey, ServerConfig(advertiseSnap2));
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            var clientTask = SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(10), CancellationToken.None, genesis, NetworkId,
                advertiseSnap2: advertiseSnap2);

            var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var ethOffset = serverConn.GetCapabilityOffset("eth");
            var ethCap = serverConn.SharedCapabilities.Find(c => c.Name == "eth");

            await serverConn.SendMessageAsync(
                ethOffset + EthMessageIds.Status,
                Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethCap!.Version,
                    NetworkId = NetworkId,
                    GenesisHash = genesis,
                    ForkHash = 0,
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = genesis
                }));

            await serverConn.ReceiveMessageAsync();

            var client = await clientTask;
            return (client, listener, serverConn);
        }

        private static Task RequestTrieNodesAsync(SyncPeerSession session) =>
            session.GetTrieNodesAsync(
                Make32(0xAA),
                new List<List<byte[]>> { new List<byte[]> { new byte[] { 0x01 } } },
                responseBytes: 1024,
                ct: CancellationToken.None);

        [Fact]
        public async Task Given_APeerNegotiatedAtSnapTwo_When_TrieNodesAreRequested_Then_ItRefusesImmediatelyInsteadOfWaitingForTheDeadline()
        {
            var (client, listener, server) = await ConnectAsync(advertiseSnap2: true);
            try
            {
                Assert.Equal(2, server.SharedCapabilities.Find(c => c.Name == "snap")?.Version);

                var watch = Stopwatch.StartNew();
                var error = await Assert.ThrowsAsync<SnapPeerCapabilityMismatchException>(() => RequestTrieNodesAsync(client));
                watch.Stop();

                Assert.Contains("snap/2", error.Message);
                Assert.True(watch.ElapsedMilliseconds < 1_000,
                    $"refusal took {watch.ElapsedMilliseconds}ms — it waited on the wire instead of refusing");
            }
            finally
            {
                await client.DisposeAsync();
                server.Dispose();
                listener.Dispose();
            }
        }

        [Fact]
        public async Task Given_APeerNegotiatedAtSnapOne_When_TrieNodesAreRequested_Then_TheRequestReachesTheWireInstead()
        {
            var (client, listener, server) = await ConnectAsync(advertiseSnap2: false);
            try
            {
                Assert.Equal(1, server.SharedCapabilities.Find(c => c.Name == "snap")?.Version);

                var error = await Record.ExceptionAsync(() => RequestTrieNodesAsync(client));

                Assert.False(
                    error is InvalidOperationException invalid && invalid.Message.Contains("snap/2"),
                    "snap/1 was refused by the snap/2 guard");
            }
            finally
            {
                await client.DisposeAsync();
                server.Dispose();
                listener.Dispose();
            }
        }
    }
}
