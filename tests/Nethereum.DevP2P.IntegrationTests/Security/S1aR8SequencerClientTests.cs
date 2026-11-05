using System;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Sequencer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1aR8SequencerClientTests
    {
        private const ulong Network = 7777;
        private static readonly byte[] Genesis = Enumerable.Repeat((byte)0xC0, 32).ToArray();

        private sealed class FakeSequencer : IAsyncDisposable
        {
            private readonly RlpxListener _listener;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private Task _serving = Task.CompletedTask;
            public EthECKey Key { get; } = EthECKey.GenerateKey();

            public FakeSequencer(Eth68StatusMessage status)
            {
                _listener = new RlpxListener(Key, new DevP2PConfig { ClientId = "fake", HandshakeTimeoutMs = 5000 });
                _listener.PeerAccepted += (_, connection) => _serving = ServeAsync(connection, status);
                _listener.Start(0, IPAddress.Loopback);
            }

            public string Enode => $"enode://{Key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{_listener.Port}";

            private async Task ServeAsync(RlpxConnection connection, Eth68StatusMessage status)
            {
                try
                {
                    var offset = connection.GetCapabilityOffset("eth");
                    await connection.SendMessageAsync(offset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(status), _cts.Token);
                    while (!_cts.IsCancellationRequested)
                    {
                        var (msgId, payload) = await connection.ReceiveMessageAsync(TimeSpan.FromSeconds(10), _cts.Token);
                        if (msgId != offset + EthMessageIds.GetBlockHeaders) continue;
                        var request = GetBlockHeadersMessageEncoder.Decode(payload);
                        await connection.SendMessageAsync(
                            offset + EthMessageIds.BlockHeaders,
                            BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage { RequestId = request.RequestId }),
                            _cts.Token);
                    }
                }
                catch
                {
                }
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                await _listener.StopAsync();
                try { await _serving; } catch { }
            }
        }

        private static Eth68StatusMessage Status(
            ulong network = Network, byte[] genesis = null, int version = 68, uint forkHash = 0u) => new Eth68StatusMessage
        {
            ProtocolVersion = version, NetworkId = network, TotalDifficulty = BigInteger.One,
            BestHash = Genesis, GenesisHash = genesis ?? Genesis, ForkHash = forkHash, ForkNext = 0
        };

        private static async Task<Exception> ConnectAsync(Eth68StatusMessage serverStatus, ulong[] forkBlocks = null)
        {
            await using var server = new FakeSequencer(serverStatus);
            var config = new DevP2PConfig
            {
                NetworkId = Network, GenesisHash = Genesis, ClientId = "client",
                ConnectTimeoutMs = 5000, HandshakeTimeoutMs = 5000, RequestTimeoutMs = 3000
            };
            await using var client = new DevP2PSequencerRpcClient(server.Enode, config, Genesis, forkBlocks);
            try
            {
                await client.GetBlockNumberAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        [Fact]
        public async Task S1aR8_03_Control_Given_AMatchingStatus_When_TheSequencerClientConnects_Then_ItIsAccepted()
        {
            var failure = await ConnectAsync(Status());

            Assert.Null(failure);
        }

        [Fact]
        public async Task S1aR8_01_Given_ASequencerOnAnotherNetwork_When_TheClientConnects_Then_ItIsRefused()
        {
            var failure = await ConnectAsync(Status(network: Network + 1));

            Assert.IsType<InvalidOperationException>(failure);
        }

        [Fact]
        public async Task S1aR8_01_Given_ASequencerWithAnotherGenesis_When_TheClientConnects_Then_ItIsRefused()
        {
            var failure = await ConnectAsync(Status(genesis: new byte[32]));

            Assert.IsType<InvalidOperationException>(failure);
        }

        [Fact]
        public async Task S1aR8_01_Given_ASequencerWithAnUnrelatedForkId_When_TheClientConnectsWithAForkSchedule_Then_ItIsRefused()
        {
            var failure = await ConnectAsync(Status(forkHash: 0xDEADBEEFu), new ulong[] { 100 });

            Assert.IsType<InvalidOperationException>(failure);
        }

        [Fact]
        public async Task S1aR8_02_Given_ASequencerWhoseStatusVersionDiffersFromTheNegotiatedOne_When_TheClientConnects_Then_ItIsRefused()
        {
            var failure = await ConnectAsync(Status(version: 67));

            Assert.IsType<InvalidOperationException>(failure);
        }
    }
}
