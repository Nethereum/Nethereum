using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.ForkId;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.SpecTests.Rlpx
{
    public class SyncPeerSessionForkIdComputationTests
    {
        private static byte[] FakeGenesis(byte salt)
        {
            var g = new byte[32];
            for (int i = 0; i < 32; i++) g[i] = (byte)(salt ^ i);
            return g;
        }

        private static async Task SendStatusFirstAsync(RlpxConnection conn, byte[] genesis, ulong networkId, uint forkHash, ulong forkNext, ulong peerLatestBlock, CancellationToken ct)
        {
            var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
            var ethOffset = conn.GetCapabilityOffset("eth");
            byte[] payload = ethCap!.Version >= 69
                ? Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethCap.Version,
                    NetworkId = networkId,
                    GenesisHash = genesis,
                    ForkHash = forkHash,
                    ForkNext = forkNext,
                    EarliestBlock = 0,
                    LatestBlock = peerLatestBlock,
                    LatestBlockHash = genesis,
                })
                : Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                {
                    ProtocolVersion = ethCap.Version,
                    NetworkId = networkId,
                    TotalDifficulty = BigInteger.One,
                    BestHash = genesis,
                    GenesisHash = genesis,
                    ForkHash = forkHash,
                    ForkNext = forkNext,
                });
            await conn.SendMessageAsync(ethOffset + EthMessageIds.Status, payload, ct);
        }

        private static (uint ForkHash, ulong ForkNext) DecodeStatus(RlpxConnection conn, int msgId, byte[] payload)
        {
            var ethOffset = conn.GetCapabilityOffset("eth");
            var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
            Assert.Equal(ethOffset + EthMessageIds.Status, msgId);
            if (ethCap!.Version >= 69)
            {
                var s = Eth69StatusMessageEncoder.Decode(payload);
                return (s.ForkHash, s.ForkNext);
            }
            else
            {
                var s = Eth68StatusMessageEncoder.Decode(payload);
                return (s.ForkHash, s.ForkNext);
            }
        }

        private static (RlpxListener Listener, string Enode, Task<(uint Hash, ulong Next)> ClientSent) StartFakePeerCapturingClientStatus(
            byte[] genesis, ulong networkId, uint peerForkHash, ulong peerForkNext, ulong peerLatestBlock)
        {
            var serverKey = EthECKey.GenerateKey();
            var clientSentTcs = new TaskCompletionSource<(uint Hash, ulong Next)>(TaskCreationOptions.RunContinuationsAsynchronously);

            var listener = new RlpxListener(serverKey, new DevP2PConfig { ClientId = "Nethereum.Spec.Tests/forkid-server" });
            listener.PeerAccepted += (_, conn) => _ = Task.Run(async () =>
            {
                try
                {
                    using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await SendStatusFirstAsync(conn, genesis, networkId, peerForkHash, peerForkNext, peerLatestBlock, ct.Token);
                    var (msgId, payload) = await conn.ReceiveMessageAsync(ct.Token);
                    clientSentTcs.TrySetResult(DecodeStatus(conn, msgId, payload));
                }
                catch (Exception ex)
                {
                    clientSentTcs.TrySetException(ex);
                }
            });
            listener.Start(0, IPAddress.Loopback);
            var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            return (listener, enode, clientSentTcs.Task);
        }

        [Fact]
        public async Task Handshake_UsesOurOwnHead_NotPeersReportedLatestBlock()
        {
            var genesis = FakeGenesis(0xD0);
            var networkId = 7790UL;
            var forkBlocks = new ulong[] { 100UL };
            var forkTimestamps = Array.Empty<ulong>();

            const ulong ourHeadBlock = 200UL;
            const ulong ourHeadTime = 0UL;
            var expected = Eip2124ForkIdCalculator.NewId(genesis, forkBlocks, forkTimestamps, ourHeadBlock, ourHeadTime);

            var peerForkHash = expected.Hash;

            var (listener, enode, clientSent) = StartFakePeerCapturingClientStatus(
                genesis, networkId, peerForkHash, peerForkNext: 0, peerLatestBlock: 0);
            using var _ = listener;

            await using var session = await SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(15), CancellationToken.None,
                genesis, networkId,
                forkBlocks: forkBlocks, forkTimestamps: forkTimestamps,
                ourHeadBlockNumber: ourHeadBlock, ourHeadTimestamp: ourHeadTime);

            var sent = await clientSent.WaitAsync(TimeSpan.FromSeconds(15));

            var genesisOnlyHash = Eip2124ForkIdCalculator.ComputeForkHash(genesis, Array.Empty<ulong>(), Array.Empty<ulong>());
            Assert.NotEqual(genesisOnlyHash, sent.Hash);
            Assert.Equal(expected.Hash, sent.Hash);
            Assert.Equal(expected.Next, sent.Next);
        }

        [Fact]
        public async Task Handshake_ComputeDisabled_FallsBackToEchoingPeerForkHash()
        {
            var genesis = FakeGenesis(0xC1);
            var networkId = 7789UL;
            const uint peerAnnouncedForkHash = 0xCAFEBABEu;

            var (listener, enode, clientSent) = StartFakePeerCapturingClientStatus(
                genesis, networkId, peerAnnouncedForkHash, peerForkNext: 0, peerLatestBlock: 42);
            using var _ = listener;

            await using var session = await SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(15), CancellationToken.None,
                genesis, networkId,
                forkBlocks: new ulong[] { 100UL }, forkTimestamps: Array.Empty<ulong>(),
                ourHeadBlockNumber: 200UL, ourHeadTimestamp: 0UL,
                computeOwnForkId: false);

            var sent = await clientSent.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.Equal(peerAnnouncedForkHash, sent.Hash);
        }

        [Fact]
        public async Task Handshake_AcceptsPeerAheadOfUs_ViaSubsetSupersetRule()
        {
            var genesis = FakeGenesis(0xD2);
            var networkId = 7792UL;
            var forkBlocks = new ulong[] { 100UL };
            var forkTimestamps = Array.Empty<ulong>();

            const ulong ourHeadBlock = 50UL;
            var peerForkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesis, forkBlocks, forkTimestamps);
            const ulong peerForkNext = 0;

            var (listener, enode, clientSent) = StartFakePeerCapturingClientStatus(
                genesis, networkId, peerForkHash, peerForkNext, peerLatestBlock: 150);
            using var _ = listener;

            await using var session = await SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(15), CancellationToken.None,
                genesis, networkId,
                forkBlocks: forkBlocks, forkTimestamps: forkTimestamps,
                ourHeadBlockNumber: ourHeadBlock, ourHeadTimestamp: 0UL);

            Assert.NotNull(session);
            await clientSent.WaitAsync(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public async Task Handshake_RejectsWrongForkPeer()
        {
            var genesis = FakeGenesis(0xD3);
            var networkId = 7793UL;
            var forkBlocks = new ulong[] { 100UL };
            var forkTimestamps = Array.Empty<ulong>();

            const uint wrongChainForkHash = 0xDEADBEEFu;

            var (listener, enode, clientSent) = StartFakePeerCapturingClientStatus(
                genesis, networkId, wrongChainForkHash, peerForkNext: 0, peerLatestBlock: 150);
            using var _ = listener;

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await SyncPeerSession.ConnectAsync(
                    enode, TimeSpan.FromSeconds(15), CancellationToken.None,
                    genesis, networkId,
                    forkBlocks: forkBlocks, forkTimestamps: forkTimestamps,
                    ourHeadBlockNumber: 50UL, ourHeadTimestamp: 0UL));
        }
    }
}
