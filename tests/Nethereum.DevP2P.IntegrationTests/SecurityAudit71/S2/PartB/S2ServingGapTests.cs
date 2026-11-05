using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.IntegrationTests.Security;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.SecurityAudit71.S2.PartB
{
    [Collection(HeavySecurityCollection.Name)]
    public class S2ServingGapTests
    {
        private sealed class RecordingHandler : IEth68RequestHandler
        {
            public readonly TaskCompletionSource<int> Receipts70Hashes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public int BalCalls;

            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());

            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockBody>>(new List<BlockBody>());

            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<List<Receipt>>());

            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
            {
                Receipts70Hashes.TrySetResult(blockHashes.Length);
                return Task.FromResult(new Receipts70Result(new List<List<Receipt>>(), false));
            }

            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());

            public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref BalCalls);
                return Task.FromResult(new List<byte[]>());
            }
        }

        private static Eth68StatusMessage Status(int version) => new Eth68StatusMessage
        {
            ProtocolVersion = version,
            NetworkId = 1,
            TotalDifficulty = BigInteger.One,
            BestHash = new byte[32],
            GenesisHash = new byte[32],
            ForkHash = 0u,
            ForkNext = 0
        };

        private static async Task<T> DriveAsync<T>(
            int negotiatedEthVersion, int remoteStatusVersion, int messageId, byte[] request,
            Func<RecordingHandler, RlpxConnection, Task<T>> observe)
        {
            var config = new DevP2PConfig { ClientId = "Nethereum/s2-serving-gap", HandshakeTimeoutMs = 5000 };
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            using var cts = new CancellationTokenSource();
            RawRlpxPeer attacker = null;
            Task loop = Task.CompletedTask;
            try
            {
                attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                var hello = RawRlpxPeer.HelloFor(attacker.Key);
                hello.Capabilities[0].Version = negotiatedEthVersion;
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                await attacker.ReadFrameAsync();
                var connection = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var handler = new RecordingHandler();
                var session = new Eth68ServerSession(connection, handler, Status(68));
                var ethOffset = connection.GetCapabilityOffset("eth");
                session.BindCapabilityOffset(ethOffset);
                session.BindRemoteStatus(Status(remoteStatusVersion));
                loop = Task.Run(async () =>
                {
                    try { await session.RunAsync(cancellationToken: cts.Token); } catch { }
                });

                await attacker.SendFrameAsync(ethOffset + messageId, request);
                return await observe(handler, connection);
            }
            finally
            {
                cts.Cancel();
                attacker?.Dispose();
                try { await loop.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                await listener.StopAsync();
            }
        }

        private static byte[][] Hashes(int count) => Enumerable.Repeat(new byte[32], count).ToArray();

        [Fact]
        public async Task S2_D2_Given_AnEth70GetReceiptsAboveTheServingCap_When_Served_Then_ItIsClampedNotDisconnected()
        {
            var count = await DriveAsync(70, 70, EthMessageIds.GetReceipts,
                GetReceiptsMessage70Encoder.Encode(new GetReceiptsMessage70 { RequestId = 1, FirstBlockReceiptIndex = 0, BlockHashes = Hashes(5000) }),
                async (h, c) =>
                {
                    var n = await h.Receipts70Hashes.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.False(c.IsDisconnected);
                    return n;
                });

            Assert.Equal(Eth68ServerSession.MaxReceiptsPerRequest, count);
        }

        [Fact]
        public async Task S2_D3_Given_AResponseCodeWithNoMatchingRequest_When_ItArrivesOnTheServingSession_Then_ThePeerIsDisconnected()
        {
            var disconnected = await DriveAsync(68, 68, Eth68MessageIds.BlockHeaders,
                new byte[] { 0xc0 },
                async (h, c) =>
                {
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (!c.IsDisconnected && DateTime.UtcNow < deadline) await Task.Delay(25);
                    return c.IsDisconnected;
                });

            Assert.True(disconnected);
        }

        [Fact]
        public async Task S2_D3_Given_AGetBlockAccessListsFromAnEth68Peer_When_Served_Then_TheHandlerIsNotCalledAndThePeerIsDisconnected()
        {
            var result = await DriveAsync(71, 68, EthMessageIds.GetBlockAccessLists,
                GetBlockAccessListsMessageEncoder.Encode(new GetBlockAccessListsMessage { RequestId = 1, BlockHashes = Hashes(3) }),
                async (h, c) =>
                {
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (!c.IsDisconnected && DateTime.UtcNow < deadline) await Task.Delay(25);
                    return (h.BalCalls, c.IsDisconnected);
                });

            Assert.Equal(0, result.BalCalls);
            Assert.True(result.IsDisconnected);
        }
    }
}
