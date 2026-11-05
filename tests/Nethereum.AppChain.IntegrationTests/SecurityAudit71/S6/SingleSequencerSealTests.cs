using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class SingleSequencerSealTests
    {
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
        private readonly ITestOutputHelper _out;

        public SingleSequencerSealTests(ITestOutputHelper output) { _out = output; }

        private static AppChainServerConfig Config(
            BigInteger chainId, EthECKey owner, EthECKey sequencer, bool holdsSequencerKey,
            EthECKey nodeKey, int port, string[] trustedPeers)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "S6Seal" };
            config.Genesis.Owner.PrivateKey = holdsSequencerKey ? owner.GetPrivateKey() : null;
            config.Genesis.Owner.Address = owner.GetPublicAddress();
            if (holdsSequencerKey) config.Consensus.Sequencer.PrivateKey = sequencer.GetPrivateKey();
            config.Consensus.Sequencer.Address = sequencer.GetPublicAddress();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = port;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = trustedPeers;
            config.Node.Network.TargetPeerCount = Math.Max(1, trustedPeers.Length);
            config.Node.Network.DialBudgetPerSecond = 0;
            config.Node.Network.MaxPeersPerIPv4Subnet = 0;
            config.Node.Network.MaxPeersPerIPv6Subnet = 0;
            config.Node.Network.MaxInboundPeers = 4;
            config.Node.Network.MaxInboundPerIP = 4;
            config.Node.Sync.Mode = SyncMode.ForwardExecute;
            config.Mud.DeployWorld = false;
            return config;
        }

        private static string Enode(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static async Task<bool> WaitAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition()) return true;
                await Task.Delay(100);
            }
            return await condition();
        }

        [Fact]
        [Trait("Finding", "S6-01")]
        public async Task Given_ASequencerAndAFollowerThatOnlyDialsThatFollower_When_TheSequencerSealsBlocks_Then_TheSecondFollowerImportsThemAndNoNodeButTheSequencerHoldsTheKey()
        {
            var chainId = new BigInteger(422100_000 + Environment.TickCount % 1000);
            var owner = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();
            var sequencerNodeKey = EthECKey.GenerateKey();
            var firstNodeKey = EthECKey.GenerateKey();
            var secondNodeKey = EthECKey.GenerateKey();
            var sequencerPort = S6ProducerHost.FreePort();
            var firstPort = S6ProducerHost.FreePort();
            var secondPort = S6ProducerHost.FreePort();

            await using var producer = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencerKey, true, sequencerNodeKey, sequencerPort, Array.Empty<string>()),
                NullLoggerFactory.Instance, CancellationToken.None);
            await using var first = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencerKey, false, firstNodeKey, firstPort,
                    new[] { Enode(sequencerNodeKey, sequencerPort) }),
                NullLoggerFactory.Instance, CancellationToken.None);
            await using var second = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencerKey, false, secondNodeKey, secondPort,
                    new[] { Enode(firstNodeKey, firstPort) }),
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.Null(first.Sequencer);
            Assert.Null(second.Sequencer);

            for (var n = 1; n <= 3; n++)
            {
                await producer.Sequencer!.ProduceBlockAsync();
                producer.ChainNode.BlockRangeAnnouncer?.Signal();
                Assert.True(await WaitAsync(async () => await second.Bundle.Blocks.GetHeightAsync() >= n, Window),
                    $"the second follower never imported block {n} relayed by the first");
            }

            var sealedHeader = await second.Bundle.Blocks.GetByNumberAsync(3);
            var extra = sealedHeader!.ExtraData!;
            var privateKeyBytes = sequencerKey.GetPrivateKey().HexToByteArray();
            _out.WriteLine($"S6 extraData={extra.Length} bytes, ends with a 65-byte seal");
            Assert.True(extra.Length >= 32 + 65);
            Assert.DoesNotContain(
                Enumerable.Range(0, Math.Max(0, extra.Length - 31))
                    .Select(i => extra.Skip(i).Take(32).SequenceEqual(privateKeyBytes)), found => found);
        }
    }
}
