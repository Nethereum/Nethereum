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
    public class S1aR7RequestClampSecurityTests
    {
        private sealed class RecordingHandler : IEth68RequestHandler
        {
            public readonly TaskCompletionSource<ulong> HeaderLimit = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<int> BodyHashes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<int> ReceiptHashes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<int> PooledHashes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<int> BalHashes = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
            {
                HeaderLimit.TrySetResult(request.Limit);
                return Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
            }

            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
            {
                BodyHashes.TrySetResult(blockHashes.Length);
                return Task.FromResult<IList<BlockBody>>(new List<BlockBody>());
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
            {
                ReceiptHashes.TrySetResult(blockHashes.Length);
                return Task.FromResult(new List<List<Receipt>>());
            }

            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
            {
                ReceiptHashes.TrySetResult(blockHashes.Length);
                return Task.FromResult(new Receipts70Result(new List<List<Receipt>>(), false));
            }

            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
            {
                PooledHashes.TrySetResult(txHashes.Length);
                return Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
            }

            public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
            {
                BalHashes.TrySetResult(blockHashes.Length);
                return Task.FromResult(new List<byte[]>());
            }
        }

        private static async Task<T> RunAsync<T>(
            Func<RecordingHandler, TaskCompletionSource<T>> observed,
            Func<int, byte[]> request,
            int messageId,
            int ethVersion = 68)
        {
            var config = new DevP2PConfig { ClientId = "Nethereum/clamp", HandshakeTimeoutMs = 5000 };
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
                hello.Capabilities[0].Version = ethVersion;
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                await attacker.ReadFrameAsync();
                var connection = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                };
                var handler = new RecordingHandler();
                var session = new Eth68ServerSession(connection, handler, status);
                var ethOffset = connection.GetCapabilityOffset("eth");
                session.BindCapabilityOffset(ethOffset);
                session.BindRemoteStatus(new Eth68StatusMessage
                {
                    ProtocolVersion = 71, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                });
                loop = Task.Run(async () =>
                {
                    try { await session.RunAsync(cancellationToken: cts.Token); } catch { }
                });

                await attacker.SendFrameAsync(ethOffset + messageId, request(0));
                var result = await observed(handler).Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(connection.IsDisconnected);
                return result;
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
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_11_Given_AGetBlockHeadersAboveTheServingCap_When_Served_Then_ItIsClampedNotDisconnected()
        {
            var limit = await RunAsync(h => h.HeaderLimit,
                _ => GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage { RequestId = 1, StartBlock = 1, Limit = 100000 }),
                EthMessageIds.GetBlockHeaders);

            Assert.Equal((ulong)Eth68ServerSession.MaxHeadersPerRequest, limit);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_11_Given_AGetBlockBodiesAboveTheServingCap_When_Served_Then_ItIsClampedNotDisconnected()
        {
            var count = await RunAsync(h => h.BodyHashes,
                _ => GetBlockBodiesMessageEncoder.Encode(new GetBlockBodiesMessage { RequestId = 1, BlockHashes = Hashes(5000) }),
                EthMessageIds.GetBlockBodies);

            Assert.Equal(Eth68ServerSession.MaxBodiesPerRequest, count);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_11_Given_AGetReceiptsAboveTheServingCap_When_Served_Then_ItIsClampedNotDisconnected()
        {
            var count = await RunAsync(h => h.ReceiptHashes,
                _ => GetReceiptsMessageEncoder.Encode(new GetReceiptsMessage { RequestId = 1, BlockHashes = Hashes(5000) }),
                EthMessageIds.GetReceipts);

            Assert.Equal(Eth68ServerSession.MaxReceiptsPerRequest, count);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_11_Given_AGetPooledTransactionsOfManyHashes_When_Served_Then_AllReachTheHandlerBecauseOnlyTheByteLimitBoundsIt()
        {
            var count = await RunAsync(h => h.PooledHashes,
                _ => GetPooledTransactionsMessageEncoder.Encode(new GetPooledTransactionsMessage { RequestId = 1, Hashes = Hashes(5000).ToList() }),
                EthMessageIds.GetPooledTransactions);

            Assert.Equal(5000, count);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_11_Given_AGetBlockAccessListsAboveTheServingCap_When_Served_Then_ItIsClampedNotDisconnected()
        {
            var count = await RunAsync(h => h.BalHashes,
                _ => GetBlockAccessListsMessageEncoder.Encode(new GetBlockAccessListsMessage { RequestId = 1, BlockHashes = Hashes(5000) }),
                EthMessageIds.GetBlockAccessLists, ethVersion: 71);

            Assert.Equal(Eth68ServerSession.MaxBlockAccessListsPerRequest, count);
        }
    }
}
