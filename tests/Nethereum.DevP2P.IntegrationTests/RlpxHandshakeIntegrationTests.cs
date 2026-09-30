using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class RlpxHandshakeIntegrationTests
    {
        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RlpxHandshakeIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task ConnectToGeth_ExchangeHello_ShareEth68()
        {
            var enode = _fixture.Enode;
            var networkId = _fixture.NetworkId;
            _output.WriteLine($"Enode: {enode}");
            _output.WriteLine($"NetworkId: {networkId}");

            var config = new DevP2PConfig
            {
                NetworkId = networkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 5000
            };

            var connector = new StaticPeerConnector(config: config);
            var conn = await connector.ConnectAsync(enode);
            try
            {
                _output.WriteLine($"Connected to: {conn.RemoteHello.ClientId}");
                Assert.True(conn.IsConnected);
                Assert.NotEmpty(conn.SharedCapabilities);

                var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
                Assert.NotNull(ethCap);
                Assert.True(ethCap.Version >= 68,
                    $"Expected eth/68+, got eth/{ethCap.Version}");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }

        [Fact]
        public async Task ConnectToGeth_ExchangeEth68Status()
        {
            var enode = _fixture.Enode;
            var networkId = _fixture.NetworkId;
            var genesisHash = _fixture.GenesisHash;
            _output.WriteLine($"Genesis hash: {genesisHash.ToHex(true)}");

            var config = new DevP2PConfig
            {
                NetworkId = networkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 5000
            };

            var connector = new StaticPeerConnector(config: config);
            var conn = await connector.ConnectAsync(enode);
            try
            {
                var ethOffset = conn.GetCapabilityOffset("eth");

                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = networkId,
                    TotalDifficulty = BigInteger.One,
                    BestHash = genesisHash,
                    GenesisHash = genesisHash,
                    ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                    ForkNext = 0
                };

                await conn.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(status));

                var (msgId, payload) = await conn.ReceiveMessageAsync();
                Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);

                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);
                Assert.Equal(networkId, remoteStatus.NetworkId);
                Assert.Equal(genesisHash.ToHex(true), remoteStatus.GenesisHash.ToHex(true));
                _output.WriteLine($"Remote status: networkId={remoteStatus.NetworkId}, TD={remoteStatus.TotalDifficulty}");
            }
            finally
            {
                await conn.DisconnectAsync();
            }
        }
    }
}
