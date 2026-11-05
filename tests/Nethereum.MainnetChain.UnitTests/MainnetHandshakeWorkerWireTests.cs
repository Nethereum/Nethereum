using System.Net;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.EVM.ForkId;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetHandshakeWorkerWireTests
    {
        private static readonly byte[] MainnetGenesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

        private static DevP2PConfig ListenerConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests/mainnet-wire-peer",
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private static async Task SendMainnetStatusAsync(RlpxConnection serverConn)
        {
            var ethOffset = serverConn.GetCapabilityOffset("eth");
            var ethCap = serverConn.SharedCapabilities.Find(c => c.Name == "eth")!;
            var ownForkId = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis,
                MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock: 0, headTime: 0);

            await serverConn.SendMessageAsync(
                ethOffset + EthMessageIds.Status,
                Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethCap.Version,
                    NetworkId = (ulong)MainnetGenesisConstants.ChainId,
                    GenesisHash = MainnetGenesis,
                    ForkHash = ownForkId.Hash,
                    ForkNext = ownForkId.Next,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = MainnetGenesis
                }));
        }

        private static async Task<(Eth69StatusMessage Status, HelloMessage Hello)> CaptureDialAsync(IPeerHandshakeWorker worker)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, ListenerConfig());
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            try
            {
                var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                var dialTask = worker.HandshakeAsync(enode, TimeSpan.FromSeconds(10), minPeerLatestBlock: 0, CancellationToken.None);

                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await SendMainnetStatusAsync(serverConn);

                var (_, payload) = await serverConn.ReceiveMessageAsync();
                var sent = Eth69StatusMessageEncoder.Decode(payload);

                var peer = await dialTask;
                if (peer is IAsyncDisposable disposablePeer) await disposablePeer.DisposeAsync();

                return (sent, serverConn.RemoteHello);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_AMainnetHandshakeWorker_When_ItDials_Then_ItAdvertisesMainnetGenesisNetworkId1AndTheMainnetForkIdentity()
        {
            var (status, _) = await CaptureDialAsync(new MainnetPeerHandshakeWorker());

            var expected = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis,
                MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock: 0, headTime: 0);
            Assert.Equal(1UL, status.NetworkId);
            Assert.Equal(MainnetGenesis, status.GenesisHash);
            Assert.Equal(expected.Hash, status.ForkHash);
            Assert.Equal(expected.Next, status.ForkNext);
        }

        [Fact]
        public async Task Given_AMainnetChainProfile_When_AskedForAWorkerWithAdvertiseSnap2True_Then_TheWorkerStillNeverAdvertisesSnap2()
        {
            var profile = new MainnetChainProfile(localKey: null, bundle: null!);

            var worker = profile.CreateHandshakeWorker(loggerFactory: null!, advertiseSnap2: true);
            var (_, hello) = await CaptureDialAsync(worker);

            Assert.IsType<MainnetPeerHandshakeWorker>(worker);
            Assert.Contains(hello.Capabilities, c => c.Name == "snap" && c.Version == 1);
            Assert.DoesNotContain(hello.Capabilities, c => c.Name == "snap" && c.Version == 2);
        }

        [Fact]
        public async Task Given_AChainHandshakeWorkerWithAdvertiseSnap2True_When_ItDials_Then_ItAdvertisesSnap2()
        {
            var forks = (MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps);
            var worker = new ChainPeerHandshakeWorker(
                MainnetGenesis, (ulong)MainnetGenesisConstants.ChainId, forks, ourHead: null!, advertiseSnap2: true);

            var (_, hello) = await CaptureDialAsync(worker);

            Assert.Contains(hello.Capabilities, c => c.Name == "snap" && c.Version == 2);
        }
    }
}
