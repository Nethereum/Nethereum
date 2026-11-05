using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class RogueProducerTests
    {
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
        private readonly ITestOutputHelper _out;

        public RogueProducerTests(ITestOutputHelper output) { _out = output; }

        private static AppChainServerConfig Config(
            BigInteger chainId, EthECKey owner, EthECKey sequencer, bool holdsSequencerKey,
            EthECKey nodeKey, int port, string[] trustedPeers, SyncMode syncMode)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "S6Rogue" };
            if (holdsSequencerKey)
            {
                config.Genesis.Owner.PrivateKey = owner.GetPrivateKey();
                config.Consensus.Sequencer.PrivateKey = sequencer.GetPrivateKey();
            }
            else
            {
                config.Genesis.Owner.Address = owner.GetPublicAddress();
                config.Consensus.Sequencer.Address = sequencer.GetPublicAddress();
            }
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
            config.Node.Sync.Mode = syncMode;
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
        [Trait("Finding", "S6-G1")]
        public async Task Given_AServingFollowerThatDialsNobody_When_AnUnknownNodeDialsItAndServesABlock_Then_TheFollowerDoesNotImportIt()
        {
            var chainId = new BigInteger(421900_000 + Environment.TickCount % 1000);
            var owner = EthECKey.GenerateKey();
            var sequencer = EthECKey.GenerateKey();
            var rogueNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var roguePort = S6ProducerHost.FreePort();
            var followerPort = S6ProducerHost.FreePort();

            await using var follower = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencer, false, followerNodeKey, followerPort,
                    Array.Empty<string>(), SyncMode.ForwardExecute),
                NullLoggerFactory.Instance, CancellationToken.None);
            var rogueConfig = Config(chainId, owner, sequencer, false, rogueNodeKey, roguePort,
                new[] { Enode(followerNodeKey, followerPort) }, SyncMode.ForwardExecute);
            await using var rogue = await AppChainComposition.ComposeAsync(rogueConfig, NullLoggerFactory.Instance, CancellationToken.None);

            var chainConfig = AppChainExecutionCore.ChainConfigFor(rogueConfig);
            var core = new AppChainExecutionCore(chainConfig, rogue.Bundle, null, NullLoggerFactory.Instance);
            var rogueProducer = new Nethereum.AppChain.Sequencer.BlockProducer(
                rogue.AppChain, core.Executor, core.StateRoot, rogue.Bundle.TrieNodes, commit: core.Commit);
            var rogueSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                rogue.AppChain,
                new SequencerConfig { SequencerAddress = sequencer.GetPublicAddress(), BlockTimeMs = 0, AllowEmptyBlocks = true },
                txPool: rogue.ChainNode.Mempool?.TxPool,
                blockProducer: rogueProducer);

            Assert.True(await WaitAsync(
                () => Task.FromResult((rogue.ChainNode.Sync?.Pool.ActivePeers.Count ?? 0) >= 1), Window),
                "the unknown node never connected to the follower");
            _out.WriteLine($"S6 followerSyncPoolPeers={follower.ChainNode.Sync?.Pool.ActivePeers.Count} followerBroadcastPeers={follower.ChainNode.Mempool?.BroadcastPool?.Count}");

            await rogueSequencer.ProduceBlockAsync();
            rogue.ChainNode.BlockRangeAnnouncer?.Signal();

            var imported = await WaitAsync(async () => await follower.Bundle.Blocks.GetHeightAsync() >= 1, Window);
            _out.WriteLine($"S6 inboundImported={imported}");

            Assert.False(imported);
        }

        [Fact]
        [Trait("Finding", "S6-G4")]
        public async Task Given_APeerWhoseGenesisDiffersFromTheFollowersConfiguredGenesis_When_ItProducesBlocks_Then_TheFollowerNeverPeersWithItOrImportsFromIt()
        {
            var chainId = new BigInteger(422000_000 + Environment.TickCount % 1000);
            var owner = EthECKey.GenerateKey();
            var configuredSequencer = EthECKey.GenerateKey();
            var otherSequencer = EthECKey.GenerateKey();
            var otherNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var otherPort = S6ProducerHost.FreePort();
            var followerPort = S6ProducerHost.FreePort();

            await using var other = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, otherSequencer, true, otherNodeKey, otherPort, Array.Empty<string>(), SyncMode.None),
                NullLoggerFactory.Instance, CancellationToken.None);
            await using var follower = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, configuredSequencer, false, followerNodeKey, followerPort,
                    new[] { Enode(otherNodeKey, otherPort) }, SyncMode.ForwardExecute),
                NullLoggerFactory.Instance, CancellationToken.None);

            await other.Sequencer!.ProduceBlockAsync();
            other.ChainNode.BlockRangeAnnouncer?.Signal();
            await Task.Delay(TimeSpan.FromSeconds(10));

            var peers = follower.ChainNode.Sync?.Pool.ActivePeers.Count ?? 0;
            var height = await follower.Bundle.Blocks.GetHeightAsync();
            _out.WriteLine($"S6 activePeers={peers} followerHeight={height}");
            Assert.Equal(0, peers);
            Assert.Equal(BigInteger.Zero, height);
        }

        [Fact]
        [Trait("Finding", "S6-G1")]
        public async Task Given_AFollowerOfTheConfiguredSequencer_When_AnotherNodeWithoutTheSequencerKeyServesItABlock_Then_TheFollowerDoesNotImportIt()
        {
            var chainId = new BigInteger(421800_000 + Environment.TickCount % 1000);
            var owner = EthECKey.GenerateKey();
            var sequencer = EthECKey.GenerateKey();
            var rogueNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var roguePort = S6ProducerHost.FreePort();
            var followerPort = S6ProducerHost.FreePort();

            await using var rogue = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencer, false, rogueNodeKey, roguePort, Array.Empty<string>(), SyncMode.None),
                NullLoggerFactory.Instance, CancellationToken.None);
            await using var follower = await AppChainComposition.ComposeAsync(
                Config(chainId, owner, sequencer, false, followerNodeKey, followerPort,
                    new[] { Enode(rogueNodeKey, roguePort) }, SyncMode.ForwardExecute),
                NullLoggerFactory.Instance, CancellationToken.None);

            var rogueConfig = Config(chainId, owner, sequencer, false, rogueNodeKey, roguePort, Array.Empty<string>(), SyncMode.None);
            var chainConfig = AppChainExecutionCore.ChainConfigFor(rogueConfig);
            var core = new AppChainExecutionCore(chainConfig, rogue.Bundle, null, NullLoggerFactory.Instance);
            var rogueProducer = new Nethereum.AppChain.Sequencer.BlockProducer(
                rogue.AppChain, core.Executor, core.StateRoot, rogue.Bundle.TrieNodes, commit: core.Commit);
            var rogueSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                rogue.AppChain,
                new SequencerConfig { SequencerAddress = sequencer.GetPublicAddress(), BlockTimeMs = 0, AllowEmptyBlocks = true },
                txPool: rogue.ChainNode.Mempool?.TxPool,
                blockProducer: rogueProducer);

            Assert.True(await WaitAsync(
                () => Task.FromResult((follower.ChainNode.Sync?.Pool.ActivePeers.Count ?? 0) >= 1), Window),
                "the follower never peered with the rogue node");

            var rogueHash = await rogueSequencer.ProduceBlockAsync();
            rogue.ChainNode.BlockRangeAnnouncer?.Signal();

            var imported = await WaitAsync(async () => await follower.Bundle.Blocks.GetHeightAsync() >= 1, Window);
            var followerHash = await follower.Bundle.Blocks.GetHashByNumberAsync(1);
            _out.WriteLine($"S6 imported={imported} rogueHash={rogueHash.ToHex()} followerHash={followerHash?.ToHex()}");

            Assert.False(imported);
        }
    }
}
