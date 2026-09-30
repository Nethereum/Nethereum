using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    public class Snap2ServerAndClientTests
    {
        private class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash, out var v) ? v : null;
        }

        private static byte[] Make32(byte fill)
        {
            var b = new byte[32];
            for (int i = 0; i < 32; i++) b[i] = (byte)(fill ^ i);
            return b;
        }

        private static DevP2PConfig BuildConfig(string clientId, bool advertiseSnap2) => new DevP2PConfig
        {
            ClientId = clientId,
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000,
            AdvertiseSnap2 = advertiseSnap2
        };

        private static async Task<(RlpxConnection clientConn, RlpxConnection serverConn, RlpxListener listener,
                int snapOffset, Task serverPumpTask, CancellationTokenSource serverCts)>
            ConnectSnapLoopbackAsync(bool advertiseSnap2, ISnapRequestHandler handler)
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/snap2-server", advertiseSnap2));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, BuildConfig("Nethereum.Spec.Tests/snap2-client", advertiseSnap2));
            await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var snapHandler = new Snap1Handler(serverConn, handler);
            var serverCts = new CancellationTokenSource();
            var serverPumpTask = Task.Run(async () =>
            {
                try
                {
                    while (!serverCts.IsCancellationRequested)
                    {
                        var (msgId, payload) = await serverConn.ReceiveMessageAsync(serverCts.Token);
                        var localId = msgId - snapHandler.SnapOffset;
                        _ = snapHandler.HandleSnapMessageAsync(localId, payload, serverCts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception) { }
            });

            var snapOffset = clientConn.GetCapabilityOffset("snap");
            return (clientConn, serverConn, listener, snapOffset, serverPumpTask, serverCts);
        }

        [Fact]
        public async Task Given_ASnap2ServerWithNoBlockAccessListStore_When_ListsAreRequested_Then_ItAnswersEmptyPositionallyPerHash()
        {
            var handler = new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), new InMemoryBytecodeStore());
            var (clientConn, _, listener, snapOffset, serverPump, serverCts) =
                await ConnectSnapLoopbackAsync(advertiseSnap2: true, handler);
            try
            {
                var hashes = new List<byte[]> { Make32(0x11), Make32(0x22), Make32(0x33) };
                var req = new GetBlockAccessListsMessage { RequestId = 7, Hashes = hashes, Bytes = 1_000_000UL };
                await clientConn.SendMessageAsync(
                    snapOffset + SnapMessageIds.GetBlockAccessLists,
                    GetBlockAccessListsMessageEncoder.Encode(req));

                var (respMsgId, respPayload) = await clientConn.ReceiveMessageAsync();
                Assert.Equal(snapOffset + SnapMessageIds.BlockAccessLists, respMsgId);

                var resp = BlockAccessListsMessageEncoder.Decode(respPayload);
                Assert.Equal(7UL, resp.RequestId);

                Assert.Equal(hashes.Count, resp.BlockAccessListsByBlock.Count);
                foreach (var entry in resp.BlockAccessListsByBlock)
                    Assert.Empty(entry);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverPump; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        private sealed class RecordingSnapHandler : ISnapRequestHandler
        {
            private readonly ISnapRequestHandler _inner;
            public GetBlockAccessListsMessage LastRequest;
            public RecordingSnapHandler(ISnapRequestHandler inner) => _inner = inner;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(request, ct);
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(request, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(request, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(request, ct);
            public Task<BlockAccessListsMessage> GetBlockAccessListsAsync(GetBlockAccessListsMessage request, CancellationToken ct = default)
            {
                LastRequest = request;
                return _inner.GetBlockAccessListsAsync(request, ct);
            }
        }

        [Fact]
        public async Task Given_ASnap2ServerHoldingBlockAccessLists_When_TheyAreRequested_Then_ItServesTheStoredBytesPositionallyPerHash()
        {
            var stored = new Nethereum.CoreChain.Storage.InMemory.InMemoryBlockAccessListStore();
            var present = Make32(0x11);
            var absent = Make32(0x22);
            var presentRlp = new byte[] { 0xc4, 0x83, 0xba, 0x1b, 0xa1 };
            await stored.SaveAsync(present, presentRlp);

            var handler = new PatriciaSnapRequestHandler(
                new InMemoryContentNodeStore(), new InMemoryBytecodeStore(), blockAccessLists: stored);
            var (clientConn, _, listener, snapOffset, serverPump, serverCts) =
                await ConnectSnapLoopbackAsync(advertiseSnap2: true, handler);
            try
            {
                var hashes = new List<byte[]> { present, absent };
                var req = new GetBlockAccessListsMessage { RequestId = 9, Hashes = hashes, Bytes = 1_000_000UL };
                await clientConn.SendMessageAsync(
                    snapOffset + SnapMessageIds.GetBlockAccessLists,
                    GetBlockAccessListsMessageEncoder.Encode(req));

                var (respMsgId, respPayload) = await clientConn.ReceiveMessageAsync();
                Assert.Equal(snapOffset + SnapMessageIds.BlockAccessLists, respMsgId);

                var resp = BlockAccessListsMessageEncoder.Decode(respPayload);
                Assert.Equal(9UL, resp.RequestId);
                Assert.Equal(2, resp.BlockAccessListsByBlock.Count);
                Assert.Equal(presentRlp, resp.BlockAccessListsByBlock[0]);
                Assert.Empty(resp.BlockAccessListsByBlock[1]);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverPump; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Snap2_ClientRequestsAndDecodes()
        {
            var recorder = new RecordingSnapHandler(
                new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), new InMemoryBytecodeStore()));
            var (clientConn, _, listener, snapOffset, serverPump, serverCts) =
                await ConnectSnapLoopbackAsync(advertiseSnap2: true, recorder);
            try
            {
                var hashes = new List<byte[]> { Make32(0xAA), Make32(0xBB) };
                var req = new GetBlockAccessListsMessage { RequestId = 55, Hashes = hashes, Bytes = 500_000UL };
                await clientConn.SendMessageAsync(
                    snapOffset + SnapMessageIds.GetBlockAccessLists,
                    GetBlockAccessListsMessageEncoder.Encode(req));

                var (respMsgId, respPayload) = await clientConn.ReceiveMessageAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(snapOffset + SnapMessageIds.BlockAccessLists, respMsgId);

                Assert.NotNull(recorder.LastRequest);
                Assert.Equal(55UL, recorder.LastRequest.RequestId);
                Assert.Equal(hashes.Count, recorder.LastRequest.Hashes.Count);
                Assert.Equal(hashes[1], recorder.LastRequest.Hashes[1]);
                Assert.Equal(500_000UL, recorder.LastRequest.Bytes);

                var resp = BlockAccessListsMessageEncoder.Decode(respPayload);
                Assert.Equal(55UL, resp.RequestId);
                Assert.Equal(hashes.Count, resp.BlockAccessListsByBlock.Count);
                foreach (var entry in resp.BlockAccessListsByBlock)
                    Assert.Empty(entry);
            }
            finally
            {
                serverCts.Cancel();
                try { await serverPump; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Snap2_RemovesTrieNodes_NotServedOnSnap2_ButStillServedOnSnap1()
        {
            var handler = new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), new InMemoryBytecodeStore());

            {
                var (clientConn, _, listener, snapOffset, serverPump, serverCts) =
                    await ConnectSnapLoopbackAsync(advertiseSnap2: true, handler);
                try
                {
                    var req = new GetTrieNodesMessage
                    {
                        RequestId = 1,
                        RootHash = Make32(0x01),
                        Paths = new List<List<byte[]>> { new() { new byte[] { 0x00 } } },
                        ResponseBytes = 4096
                    };
                    await clientConn.SendMessageAsync(
                        snapOffset + SnapMessageIds.GetTrieNodes,
                        GetTrieNodesMessageEncoder.Encode(req));

                    var receiveTask = clientConn.ReceiveMessageAsync();
                    var winner = await Task.WhenAny(receiveTask, Task.Delay(TimeSpan.FromMilliseconds(750)));
                    Assert.NotSame(receiveTask, winner);
                }
                finally
                {
                    serverCts.Cancel();
                    try { await serverPump; } catch { }
                    try { clientConn.Dispose(); } catch { }
                    await listener.StopAsync();
                }
            }

            {
                var (clientConn, _, listener, snapOffset, serverPump, serverCts) =
                    await ConnectSnapLoopbackAsync(advertiseSnap2: false, handler);
                try
                {
                    var req = new GetTrieNodesMessage
                    {
                        RequestId = 2,
                        RootHash = Make32(0x01),
                        Paths = new List<List<byte[]>> { new() { new byte[] { 0x00 } } },
                        ResponseBytes = 4096
                    };
                    await clientConn.SendMessageAsync(
                        snapOffset + SnapMessageIds.GetTrieNodes,
                        GetTrieNodesMessageEncoder.Encode(req));

                    var (respMsgId, respPayload) = await clientConn.ReceiveMessageAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal(snapOffset + SnapMessageIds.TrieNodes, respMsgId);
                    var resp = TrieNodesMessageEncoder.Decode(respPayload);
                    Assert.Equal(2UL, resp.RequestId);
                }
                finally
                {
                    serverCts.Cancel();
                    try { await serverPump; } catch { }
                    try { clientConn.Dispose(); } catch { }
                    await listener.StopAsync();
                }
            }
        }
    }
}
