using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.SecurityAudit71.S2.PartB
{
    public class S2SnapResponseBoundTests
    {
        private const ulong NetworkId = 1337;

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (var i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static DevP2PConfig Config(bool snap2) => new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests/s2-snap-bound",
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 5_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000,
            AdvertiseSnap2 = snap2
        };

        private static async Task<(SyncPeerSession Client, RlpxListener Listener, RlpxConnection Server)> ConnectAsync(bool snap2)
        {
            var serverKey = EthECKey.GenerateKey();
            var genesis = Make32(0xC0);
            var listener = new RlpxListener(serverKey, Config(snap2));
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            var clientTask = SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(10), CancellationToken.None, genesis, NetworkId, advertiseSnap2: snap2);

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

        private static async Task<Exception> RequestAndAnswerOversizeAsync<T>(
            Func<SyncPeerSession, Task<T>> request,
            int requestSnapId,
            Func<byte[], int> requestIdOf,
            int responseSnapId,
            Func<ulong, byte[]> oversizeResponse,
            bool snap2 = false)
        {
            var (client, listener, server) = await ConnectAsync(snap2);
            try
            {
                var snapOffset = server.GetCapabilityOffset("snap");
                var pending = request(client);

                var (msgId, payload) = await server.ReceiveMessageAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(snapOffset + requestSnapId, msgId);
                var requestId = (ulong)requestIdOf(payload);
                await server.SendMessageAsync(snapOffset + responseSnapId, oversizeResponse(requestId));

                return await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(20)));
            }
            finally
            {
                await client.DisposeAsync();
                server.Dispose();
                listener.Dispose();
            }
        }

        [Fact]
        public async Task S2_C3_Given_AByteCodesReplyWithMoreCodesThanRequested_When_TheClientReceivesIt_Then_TheReplyIsRefused()
        {
            var error = await RequestAndAnswerOversizeAsync(
                s => s.GetByteCodesAsync(new List<byte[]> { Make32(1), Make32(2), Make32(3) }, 1024, CancellationToken.None),
                SnapMessageIds.GetByteCodes,
                p => (int)GetByteCodesMessageEncoder.Decode(p).RequestId,
                SnapMessageIds.ByteCodes,
                id => ByteCodesMessageEncoder.Encode(new ByteCodesMessage
                {
                    RequestId = id,
                    Codes = Enumerable.Range(0, 50).Select(i => new byte[] { 0x60, (byte)i }).ToList()
                }));

            Assert.NotNull(error);
        }

        [Fact]
        public async Task S2_C4_Given_ATrieNodesReplyWithMoreNodesThanRequestedPaths_When_TheClientReceivesIt_Then_TheReplyIsRefused()
        {
            var error = await RequestAndAnswerOversizeAsync(
                s => s.GetTrieNodesAsync(Make32(0xAA), new List<List<byte[]>> { new List<byte[]> { new byte[] { 0x01 } } }, 1024, CancellationToken.None),
                SnapMessageIds.GetTrieNodes,
                p => (int)GetTrieNodesMessageEncoder.Decode(p).RequestId,
                SnapMessageIds.TrieNodes,
                id => TrieNodesMessageEncoder.Encode(new TrieNodesMessage
                {
                    RequestId = id,
                    Nodes = Enumerable.Range(0, 40).Select(i => new byte[] { 0xc0 }).ToList()
                }));

            Assert.NotNull(error);
        }

        [Fact]
        public async Task S2_C5_Given_AnAccountRangeReplyFarAboveTheRequestedResponseBytes_When_TheClientReceivesIt_Then_TheReplyIsRefused()
        {
            var error = await RequestAndAnswerOversizeAsync(
                s => s.GetAccountRangeAsync(Make32(0xAA), new byte[32], Make32(0xFF), 1024, CancellationToken.None),
                SnapMessageIds.GetAccountRange,
                p => (int)GetAccountRangeMessageEncoder.Decode(p).RequestId,
                SnapMessageIds.AccountRange,
                id =>
                {
                    var reply = new AccountRangeMessage { RequestId = id };
                    var body = Nethereum.RLP.RLP.EncodeList(
                        Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                        Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                        Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                        Nethereum.RLP.RLP.EncodeElement(new byte[0]));
                    for (var i = 0; i < 2000; i++)
                    {
                        var hash = new byte[32];
                        hash[30] = (byte)(i >> 8);
                        hash[31] = (byte)i;
                        reply.Accounts.Add(new AccountRangeMessage.AccountEntry { Hash = hash, Body = body });
                    }
                    return AccountRangeMessageEncoder.Encode(reply);
                });

            Assert.NotNull(error);
        }

        [Fact]
        public async Task S2_C7_Given_ASnapBalReplyWithMoreEntriesThanRequestedHashes_When_TheClientReceivesIt_Then_TheReplyIsRefused()
        {
            var error = await RequestAndAnswerOversizeAsync(
                s => s.GetBlockAccessListsAsync(new List<byte[]> { Make32(1), Make32(2) }, 4096, CancellationToken.None),
                SnapMessageIds.GetBlockAccessLists,
                p => (int)Nethereum.Model.P2P.Snap.GetBlockAccessListsMessageEncoder.Decode(p).RequestId,
                SnapMessageIds.BlockAccessLists,
                id => Nethereum.Model.P2P.Snap.BlockAccessListsMessageEncoder.Encode(new Nethereum.Model.P2P.Snap.BlockAccessListsMessage
                {
                    RequestId = id,
                    BlockAccessListsByBlock = Enumerable.Range(0, 40).Select(i => new byte[] { 0xc0 }).ToList()
                }),
                snap2: true);

            Assert.NotNull(error);
        }
    }
}
