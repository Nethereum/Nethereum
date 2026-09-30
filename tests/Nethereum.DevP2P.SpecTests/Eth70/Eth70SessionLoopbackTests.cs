using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.SpecTests.Eth70
{
    public class Eth70SessionLoopbackTests
    {
        private const ulong NetworkId = 7777ul;

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static DevP2PConfig BuildConfig(string clientId) => new DevP2PConfig
        {
            ClientId = clientId,
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private static async Task<(SyncPeerSession client, RlpxListener listener,
            Eth68ServerSession serverSession, Task serverLoop, CancellationTokenSource serverCts)>
            ConnectLoopbackAsync(IEth68RequestHandler handler)
        {
            var serverKey = EthECKey.GenerateKey();
            var genesis = Make32(0xC0);

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/eth70-server"));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            var clientConnectTask = SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(10), CancellationToken.None, genesis, NetworkId);

            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var serverEthOffset = serverConn.GetCapabilityOffset("eth");
            var ethCap = serverConn.SharedCapabilities.Find(c => c.Name == "eth");
            Assert.NotNull(ethCap);
            Assert.Equal(71, ethCap!.Version);

            await serverConn.SendMessageAsync(
                serverEthOffset + EthMessageIds.Status,
                Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = 71,
                    NetworkId = NetworkId,
                    GenesisHash = genesis,
                    ForkHash = 0,
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = genesis
                }));

            var (clientStatusMsgId, clientStatusPayload) = await serverConn.ReceiveMessageAsync();
            Assert.Equal(serverEthOffset + EthMessageIds.Status, clientStatusMsgId);
            var clientStatus = Eth69StatusMessageEncoder.Decode(clientStatusPayload);

            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 71,
                NetworkId = NetworkId,
                GenesisHash = genesis,
                BestHash = genesis,
                TotalDifficulty = System.Numerics.BigInteger.One,
                ForkHash = 0,
                ForkNext = 0
            };
            var serverSession = new Eth68ServerSession(serverConn, handler, localStatus);
            serverSession.BindCapabilityOffset(serverEthOffset);
            typeof(Eth68ServerSession)
                .GetProperty(nameof(Eth68ServerSession.RemoteStatus))!
                .SetValue(serverSession, new Eth68StatusMessage
                {
                    ProtocolVersion = 71,
                    NetworkId = clientStatus.NetworkId,
                    GenesisHash = clientStatus.GenesisHash,
                    BestHash = clientStatus.LatestBlockHash,
                    ForkHash = clientStatus.ForkHash,
                    ForkNext = clientStatus.ForkNext
                });

            var serverCts = new CancellationTokenSource();
            var serverLoop = Task.Run(async () =>
            {
                try { await serverSession.RunAsync(cancellationToken: serverCts.Token); }
                catch { }
            });

            var client = await clientConnectTask;
            Assert.Equal(71, client.EthVersion);

            return (client, listener, serverSession, serverLoop, serverCts);
        }

        [Fact]
        public async Task Eth71Session_UsesReceipts70Format_NotEth69()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var handler = new StorageBackedEth68Handler(bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Withdrawals);
            var (client, listener, _, serverLoop, serverCts) = await ConnectLoopbackAsync(handler);
            try
            {
                var hashes = new List<byte[]> { Make32(0x01), Make32(0x02) };

                var ethOffset = client.Connection.GetCapabilityOffset("eth");
                var reqId = client.Connection.NextRequestId();
                var request = new GetReceiptsMessage70
                {
                    RequestId = reqId,
                    FirstBlockReceiptIndex = 0,
                    BlockHashes = hashes.ToArray()
                };
                var payload = await client.Connection.SendRequestAsync(
                    ethOffset + EthMessageIds.GetReceipts,
                    GetReceiptsMessage70Encoder.Encode(request),
                    ethOffset + EthMessageIds.Receipts, reqId, TimeSpan.FromSeconds(10), CancellationToken.None);

                var decoded = ReceiptsMessageEth70Encoder.Decode(payload);
                Assert.Equal(reqId, decoded.RequestId);
                Assert.False(decoded.LastBlockIncomplete);
                Assert.Empty(decoded.ReceiptsByBlock);

                var ex = Record.Exception(() => ReceiptsMessageEth69Encoder.Decode(payload));
                Assert.NotNull(ex);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverLoop; } catch { }
                await client.DisposeAsync();
                await bundle.DisposeAsync();
                await listener.StopAsync();
            }
        }

        private sealed class ScriptedReceipts70Handler : IEth68RequestHandler
        {
            private readonly List<Receipt> _allReceiptsForBlock;
            private int _callCount;
            public List<ulong> ObservedFirstIndices { get; } = new();

            public ScriptedReceipts70Handler(List<Receipt> allReceiptsForBlock)
            {
                _allReceiptsForBlock = allReceiptsForBlock;
            }

            public Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
            {
                ObservedFirstIndices.Add(firstBlockReceiptIndex);
                _callCount++;
                var start = (int)firstBlockReceiptIndex;

                if (_callCount == 1)
                {
                    var partial = _allReceiptsForBlock.GetRange(start, 2);
                    return Task.FromResult(new Receipts70Result(new List<List<Receipt>> { partial }, true));
                }

                var rest = _allReceiptsForBlock.GetRange(start, _allReceiptsForBlock.Count - start);
                return Task.FromResult(new Receipts70Result(new List<List<Receipt>> { rest }, false));
            }

            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());
            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockBody>>(new List<BlockBody>());
            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<List<Receipt>>());
            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
            public Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<byte[]>());
        }

        private static Receipt MakeReceipt(byte statusSeed) => new Receipt
        {
            PostStateOrStatus = new byte[] { statusSeed },
            CumulativeGasUsed = new Nethereum.Util.EvmUInt256(21000UL + statusSeed),
            Bloom = new byte[256],
            Logs = new List<Log>()
        };

        [Fact]
        public async Task Eth70Client_ContinuesOnIncomplete_UntilComplete()
        {
            var allReceipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3), MakeReceipt(4), MakeReceipt(5) };
            var handler = new ScriptedReceipts70Handler(allReceipts);
            var (client, listener, _, serverLoop, serverCts) = await ConnectLoopbackAsync(handler);
            try
            {
                var hashes = new List<byte[]> { Make32(0xAA) };

                var result = await client.GetReceiptsAsync(hashes, CancellationToken.None);

                Assert.Equal(1, result.Count);
                Assert.Equal(5, result[0].Count);
                for (int i = 0; i < 5; i++)
                    Assert.Equal(allReceipts[i].PostStateOrStatus, result[0][i].PostStateOrStatus);

                Assert.Equal(2, handler.ObservedFirstIndices.Count);
                Assert.Equal(0ul, handler.ObservedFirstIndices[0]);
                Assert.Equal(2ul, handler.ObservedFirstIndices[1]);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverLoop; } catch { }
                await client.DisposeAsync();
                await listener.StopAsync();
            }
        }
    }
}
