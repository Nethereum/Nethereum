using Nethereum.EVM;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.EVM.ForkId;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainPeerHandshakeWorkerOutboundForkIdTests
    {
        private const ulong NetworkId = 555555UL;

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (var i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static DevP2PConfig BuildDialerConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests/w0b-dummy-peer",
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private static async Task<InMemoryChainStoreBundle> BundleWithHeadAsync(
            byte[] genesisHash, long genesisTimestamp, ulong? headBlock = null, long? headTimestamp = null)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0, Timestamp = genesisTimestamp }, genesisHash);
            if (headBlock.HasValue)
                await bundle.Blocks.SaveAsync(
                    new BlockHeader { BlockNumber = headBlock.Value, Timestamp = headTimestamp!.Value },
                    Make32((byte)headBlock.Value));
            return bundle;
        }

        private static async Task<PeerListener> StartInboundListenerAsync(IChainProfile profile, IChainStoreBundle bundle)
        {
            var config = new ChainNodeConfig();
            config.Network.ListenPort = 0;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.NodeKeyHex = EthECKey.GenerateKey().GetPrivateKey();

            return await ChainNodeServeListener.StartAsync(
                profile, bundle, config, mempool: null, callbacks: null, loggerFactory: null);
        }

        private static async Task<uint> CaptureInboundForkHashAsync(PeerListener listener, byte[] genesisHash, ulong networkId)
        {
            var enode = $"enode://{listener.NodeId.ToHex()}@127.0.0.1:{listener.Port}";
            var peer = await SyncPeerSession.ConnectAsync(
                enode, TimeSpan.FromSeconds(10), CancellationToken.None, genesisHash, networkId);
            try
            {
                return peer.PeerForkHash;
            }
            finally
            {
                await peer.DisposeAsync();
            }
        }

        private static async Task<(uint ForkHash, ulong ForkNext)> CaptureOutboundStatusAsync(
            IPeerHandshakeWorker worker, byte[] genesisHash, ulong networkId,
            uint peerForkHash = 0, ulong peerForkNext = 0)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, BuildDialerConfig());
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            try
            {
                var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                var dialTask = worker.HandshakeAsync(enode, TimeSpan.FromSeconds(10), minPeerLatestBlock: 0, CancellationToken.None);

                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var ethOffset = serverConn.GetCapabilityOffset("eth");
                var ethCap = serverConn.SharedCapabilities.Find(c => c.Name == "eth");

                await serverConn.SendMessageAsync(
                    ethOffset + EthMessageIds.Status,
                    Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                    {
                        ProtocolVersion = ethCap!.Version,
                        NetworkId = networkId,
                        GenesisHash = genesisHash,
                        ForkHash = peerForkHash,
                        ForkNext = peerForkNext,
                        EarliestBlock = 0,
                        LatestBlock = 0,
                        LatestBlockHash = genesisHash
                    }));

                var (_, payload) = await serverConn.ReceiveMessageAsync();
                var sentStatus = Eth69StatusMessageEncoder.Decode(payload);

                var peer = await dialTask;
                if (peer is IAsyncDisposable disposablePeer) await disposablePeer.DisposeAsync();

                return (sentStatus.ForkHash, sentStatus.ForkNext);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        private sealed class ConfigurableChainProfile : IChainProfile
        {
            public ConfigurableChainProfile(
                ulong networkId, byte[] genesisHash, (ulong[] BlockHeights, ulong[] Timestamps) forkThresholds)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
                ForkThresholds = forkThresholds;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds { get; }

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                throw new NotSupportedException(
                    "These tests construct the outbound ChainPeerHandshakeWorker directly to control forkThresholds/ourHead.");
        }

        [Fact]
        public async Task Given_ANodeWithThresholdsAndABundle_When_ItDialsOutbound_Then_ItAdvertisesTheSameForkIdItServesInbound()
        {
            var genesisHash = Make32(0xA1);
            var thresholds = (BlockHeights: new ulong[] { 10, 50 }, Timestamps: Array.Empty<ulong>());
            var bundle = await BundleWithHeadAsync(genesisHash, genesisTimestamp: 1000, headBlock: 15, headTimestamp: 15000);

            var profile = new ConfigurableChainProfile(NetworkId, genesisHash, thresholds);
            var listener = await StartInboundListenerAsync(profile, bundle);
            try
            {
                var inboundForkHash = await CaptureInboundForkHashAsync(listener, genesisHash, NetworkId);

                var expected = Eip2124ForkIdCalculator.NewId(
                    genesisHash, thresholds.BlockHeights, thresholds.Timestamps, headBlock: 15, headTime: 15000);

                var peerForkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesisHash, Array.Empty<ulong>(), Array.Empty<ulong>());
                const ulong peerForkNext = 10;

                var worker = new ChainPeerHandshakeWorker(
                    genesisHash, NetworkId, thresholds, () => ChainHeadResolver.ResolveOurHeadAsync(bundle));
                var (outboundForkHash, _) = await CaptureOutboundStatusAsync(
                    worker, genesisHash, NetworkId, peerForkHash, peerForkNext);

                Assert.NotEqual(peerForkHash, expected.Hash);
                Assert.Equal(expected.Hash, inboundForkHash);
                Assert.Equal(expected.Hash, outboundForkHash);
            }
            finally
            {
                await listener.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ANodeWhoseHeadHasCrossedAForkBoundary_When_ItDialsAgain_Then_TheForkIdReflectsTheNewHead()
        {
            var genesisHash = Make32(0xB2);
            var thresholds = (BlockHeights: new ulong[] { 10 }, Timestamps: Array.Empty<ulong>());
            var bundle = await BundleWithHeadAsync(genesisHash, genesisTimestamp: 1000, headBlock: 5, headTimestamp: 5000);

            var worker = new ChainPeerHandshakeWorker(
                genesisHash, NetworkId, thresholds, () => ChainHeadResolver.ResolveOurHeadAsync(bundle));

            var beforeExpected = Eip2124ForkIdCalculator.NewId(
                genesisHash, thresholds.BlockHeights, thresholds.Timestamps, headBlock: 5, headTime: 5000);
            var beforePeerForkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesisHash, thresholds.BlockHeights, Array.Empty<ulong>());
            var (beforeHash, beforeNext) = await CaptureOutboundStatusAsync(
                worker, genesisHash, NetworkId, beforePeerForkHash, peerForkNext: 0);

            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 15, Timestamp = 15000 }, Make32(15));

            var afterExpected = Eip2124ForkIdCalculator.NewId(
                genesisHash, thresholds.BlockHeights, thresholds.Timestamps, headBlock: 15, headTime: 15000);
            var afterPeerForkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesisHash, Array.Empty<ulong>(), Array.Empty<ulong>());
            var (afterHash, afterNext) = await CaptureOutboundStatusAsync(
                worker, genesisHash, NetworkId, afterPeerForkHash, peerForkNext: 10);

            Assert.NotEqual(beforePeerForkHash, beforeExpected.Hash);
            Assert.NotEqual(afterPeerForkHash, afterExpected.Hash);

            Assert.Equal(beforeExpected.Hash, beforeHash);
            Assert.Equal(beforeExpected.Next, beforeNext);
            Assert.Equal(afterExpected.Hash, afterHash);
            Assert.Equal(0UL, afterNext);
            Assert.NotEqual(beforeHash, afterHash);

            await bundle.DisposeAsync();
        }

        [Fact]
        public async Task Given_AChainThatDeclaresNoForkThresholds_When_ItDialsOutbound_Then_ItStillHasNoOpinionAndEchoesThePeer()
        {
            var genesisHash = Make32(0xC3);
            var emptyThresholds = (BlockHeights: Array.Empty<ulong>(), Timestamps: Array.Empty<ulong>());
            var bundle = await BundleWithHeadAsync(genesisHash, genesisTimestamp: 1000, headBlock: 100, headTimestamp: 100000);

            var worker = new ChainPeerHandshakeWorker(
                genesisHash, NetworkId, emptyThresholds, () => ChainHeadResolver.ResolveOurHeadAsync(bundle));

            const uint peerForkHash = 0xDEADBEEF;
            const ulong peerForkNext = 777;

            var (sentHash, sentNext) = await CaptureOutboundStatusAsync(
                worker, genesisHash, NetworkId, peerForkHash, peerForkNext);

            Assert.Equal(peerForkHash, sentHash);
            Assert.Equal(0UL, sentNext);

            await bundle.DisposeAsync();
        }

        [Fact]
        public async Task Given_AProfileAndABundle_When_TheHeadIsResolved_Then_OneResolverServesBothDirections()
        {
            var genesisHash = Make32(0xD4);
            const long distinctiveGenesisTimestamp = 424242;
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0, Timestamp = distinctiveGenesisTimestamp }, genesisHash);

            var directResolve = await ChainHeadResolver.ResolveOurHeadAsync(bundle);

            Assert.Equal((0UL, (ulong)distinctiveGenesisTimestamp), directResolve);
            Assert.NotEqual(0UL, directResolve.HeadTime);
            Assert.NotEqual(1438269973UL, directResolve.HeadTime);

            var profile = new DefaultChainProfile(NetworkId, genesisHash, ChainForkSchedule.Running((long)NetworkId, HardforkName.Amsterdam), Array.Empty<string>(), bundle: bundle);
            var worker = profile.CreateHandshakeWorker(loggerFactory: null, advertiseSnap2: false);

            var ourHeadField = typeof(ChainPeerHandshakeWorker)
                .GetField("_ourHead", BindingFlags.NonPublic | BindingFlags.Instance);
            var ourHead = (Func<Task<(ulong HeadBlock, ulong HeadTime)>>)ourHeadField!.GetValue(worker)!;
            var viaWorker = await ourHead();

            Assert.Equal(directResolve, viaWorker);

            await bundle.DisposeAsync();
        }
    }
}
