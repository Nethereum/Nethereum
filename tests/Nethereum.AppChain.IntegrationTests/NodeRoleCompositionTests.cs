using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class NodeRoleCompositionTests
    {
        private static readonly TimeSpan ImportTimeout = TimeSpan.FromSeconds(60);

        [Fact]
        public async Task Given_ASigningKeyAndTrustedPeers_When_TheRunnerComposes_Then_ItBothProducesAndImports()
        {
            var scenario = await TwoNodeScenario.StartAsync(subjectHasSigningKey: true);
            await using var _ = scenario;

            Assert.True(scenario.Subject.Produces, "a node with a signing key must be marked as producing");
            Assert.True(scenario.Subject.Imports, "a node whose Sync.Mode is not None must be marked as importing");
            Assert.NotNull(scenario.Subject.Sequencer);

            var sealedBySibling = await scenario.Sibling.Sequencer.ProduceBlockAsync();
            Assert.NotNull(sealedBySibling);

            var imported = await WaitUntilAsync(
                async () => await scenario.Subject.Bundle.Blocks.GetHeightAsync() >= 1,
                ImportTimeout);
            Assert.True(imported, "the signing subject never imported the sibling's sealed block");

            var subjectBlock1Hash = await scenario.Subject.Bundle.Blocks.GetHashByNumberAsync(1);
            Assert.True(ByteUtil.AreEqual(subjectBlock1Hash, sealedBySibling),
                "the subject's block 1 is not the sibling's sealed block, so this was not a real import");

            var sealedBySubject = await scenario.Subject.Sequencer.ProduceBlockAsync();
            Assert.NotNull(sealedBySubject);

            var subjectHeightAfterOwnSeal = await scenario.Subject.Bundle.Blocks.GetHeightAsync();
            Assert.Equal(new BigInteger(2), subjectHeightAfterOwnSeal);

            var ownBlockHeader = await scenario.Subject.Bundle.Blocks.GetByNumberAsync(2);
            Assert.True(ByteUtil.AreEqual(ownBlockHeader.ParentHash, subjectBlock1Hash),
                "the subject's own sealed block did not build on the block it imported");
        }

        [Fact]
        public async Task Given_NoSigningKeyAndTrustedPeers_When_TheRunnerComposes_Then_ItImportsAndNeverProduces()
        {
            var scenario = await TwoNodeScenario.StartAsync(subjectHasSigningKey: false);
            await using var _ = scenario;

            Assert.False(scenario.Subject.Produces, "a node with no signing key must never be marked as producing");
            Assert.Null(scenario.Subject.Sequencer);
            Assert.True(scenario.Subject.Imports, "a keyless node must still be marked as importing");

            var sealedBySibling = await scenario.Sibling.Sequencer.ProduceBlockAsync();
            Assert.NotNull(sealedBySibling);

            var imported = await WaitUntilAsync(
                async () => await scenario.Subject.Bundle.Blocks.GetHeightAsync() >= 1,
                ImportTimeout);
            Assert.True(imported, "the keyless follower never imported the sibling's sealed block");

            var subjectBlock1Hash = await scenario.Subject.Bundle.Blocks.GetHashByNumberAsync(1);
            Assert.True(ByteUtil.AreEqual(subjectBlock1Hash, sealedBySibling),
                "the subject's block 1 is not the sibling's sealed block, so this was not a real import");
        }

        [Fact]
        public async Task Given_ARunnerWithASigningKey_When_TheNodeConfigIsBuilt_Then_TheMempoolAxesAreTheSignerRoles()
        {
            var config = IsolatedNodeConfig(hasSigningKey: true);

            await using var node = await ComposedAppChainNode.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.True(node.Produces);
            Assert.Equal(MempoolRetention.Full, config.Node.Mempool.Retention);
            Assert.Equal(MempoolRelay.Ours, config.Node.Mempool.Relay);
        }

        [Fact]
        public async Task Given_ARunnerWithNoSigningKey_When_TheNodeConfigIsBuilt_Then_TheMempoolAxesAreTheFollowerRoles()
        {
            var config = IsolatedNodeConfig(hasSigningKey: false);

            await using var node = await ComposedAppChainNode.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.False(node.Produces);
            Assert.Equal(MempoolRetention.RelayOnly, config.Node.Mempool.Retention);
            Assert.Equal(MempoolRelay.Ours, config.Node.Mempool.Relay);
        }

        [Fact]
        public async Task Given_ALeaseHolderThatDies_When_TheArbiterRevokesIt_Then_TheSiblingTakesOverWithoutADoubleSealOrAReorg()
        {
            var scenario = await HaTwoNodeScenario.StartAsync();
            await using var _ = scenario;

            byte[]? lastHashSealedByA = null;
            for (var i = 0; i < 3; i++)
            {
                lastHashSealedByA = await scenario.A.Sequencer.ProduceBlockAsync();
            }

            var heightAfterA = await scenario.A.Bundle.Blocks.GetHeightAsync();
            Assert.Equal(new BigInteger(3), heightAfterA);

            var bWasHotBeforeRevoke = await WaitUntilAsync(
                async () => await scenario.B.Bundle.Blocks.GetHeightAsync() >= heightAfterA, ImportTimeout);
            Assert.True(bWasHotBeforeRevoke, "B never caught up to A over BAL/DevP2P before the revoke - not a hot handoff");

            scenario.Arbiter.ForceRevoke(scenario.ANodeId);

            var bAcquired = await WaitUntilAsync(async () =>
            {
                await scenario.BAuthority.PollOnceAsync();
                return scenario.BAuthority.CurrentProducer() == scenario.BNodeId;
            }, TimeSpan.FromSeconds(10));
            Assert.True(bAcquired, "B never acquired the lease after A was revoked");

            var sealedByB = await scenario.B.Sequencer.ProduceBlockAsync();
            Assert.NotNull(sealedByB);

            var bHeader = await scenario.B.Bundle.Blocks.GetByNumberAsync(4);
            Assert.True(ByteUtil.AreEqual(bHeader.ParentHash, lastHashSealedByA),
                "B's takeover block did not build on A's last sealed block - this was a reorg, not a handoff");

            await scenario.AAuthority.PollOnceAsync();
            await Assert.ThrowsAsync<SequencerLeaseNotHeldException>(() => scenario.A.Sequencer.ProduceBlockAsync());

            var heightAfterFailover = await scenario.A.Bundle.Blocks.GetHeightAsync();
            Assert.Equal(new BigInteger(3), heightAfterFailover);
        }

        [Fact]
        public async Task NonVacuityTwin_DoubleSeal_Given_TheGateIsBypassed_When_TheFormerHolderSealsAfterHandoff_Then_ItWronglySucceeds()
        {
            var scenario = await HaTwoNodeScenario.StartAsync();
            await using var _ = scenario;

            await scenario.A.Sequencer.ProduceBlockAsync();

            var bypassedSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                scenario.A.AppChain,
                new SequencerConfig(),
                blockProductionStrategy: new DefaultBlockProductionStrategy(
                    new ChainConfig { ChainId = scenario.A.AppChain.Config.ChainId }));

            scenario.Arbiter.ForceRevoke(scenario.ANodeId);
            await scenario.BAuthority.PollOnceAsync();

            Exception? threw = null;
            try
            {
                await bypassedSequencer.ProduceBlockAsync();
            }
            catch (Exception ex)
            {
                threw = ex;
            }

            Assert.Null(threw);
        }

        [Fact]
        public async Task NonVacuityTwin_ColdSync_Given_BNeverCaughtUp_When_CheckedForAHotHandoff_Then_TheHotAssertionFails()
        {
            var scenario = await HaTwoNodeScenario.StartAsync(bImports: false);
            await using var _ = scenario;

            await scenario.A.Sequencer.ProduceBlockAsync();
            await scenario.A.Sequencer.ProduceBlockAsync();

            var heightAfterA = await scenario.A.Bundle.Blocks.GetHeightAsync();

            var bWasHotBeforeRevoke = await WaitUntilAsync(
                async () => await scenario.B.Bundle.Blocks.GetHeightAsync() >= heightAfterA, TimeSpan.FromSeconds(3));

            Assert.False(bWasHotBeforeRevoke, "twin expects B to still be cold: its follower sync was disabled");
        }

        private static AppChainServerConfig IsolatedNodeConfig(bool hasSigningKey)
        {
            var chainId = new BigInteger(420420_500 + Environment.TickCount % 1000);
            var genesisOwnerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();

            var config = new AppChainServerConfig
            {
                ChainId = chainId,
                ChainName = "NodeRoleCompositionE2E-Isolated"
            };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            if (hasSigningKey)
                config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            else
                config.Consensus.Sequencer.Address = sequencerKey.GetPublicAddress();

            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = false;
            config.Node.Network.ListenPort = 0;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Sync.Mode = SyncMode.None;

            return config;
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false)) return true;
                await Task.Delay(200).ConfigureAwait(false);
            }
            return await condition().ConfigureAwait(false);
        }

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, bool hasSigningKey,
            EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
            var config = new AppChainServerConfig
            {
                ChainId = chainId,
                ChainName = "NodeRoleCompositionE2E"
            };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            if (hasSigningKey)
                config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            else
                config.Consensus.Sequencer.Address = sequencerKey.GetPublicAddress();

            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = listenPort;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = trustedPeers;
            config.Node.Network.TargetPeerCount = Math.Max(1, trustedPeers.Length);
            config.Node.Network.DialBudgetPerSecond = 0;
            config.Node.Network.MaxPeersPerIPv4Subnet = 0;
            config.Node.Network.MaxPeersPerIPv6Subnet = 0;
            config.Node.Network.MaxInboundPeers = 4;
            config.Node.Network.MaxInboundPerIP = 4;
            config.Node.Sync.Mode = SyncMode.ForwardExecute;

            return config;
        }

        private static string EnodeOf(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static IReadOnlyList<int> ReserveFreeLoopbackPorts(int count)
        {
            var ports = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
                listener.Stop();
            }
            return ports;
        }

        private sealed class TwoNodeScenario : IAsyncDisposable
        {
            private TwoNodeScenario(ComposedAppChainNode subject, ComposedAppChainNode sibling)
            {
                Subject = subject;
                Sibling = sibling;
            }

            public ComposedAppChainNode Subject { get; }

            public ComposedAppChainNode Sibling { get; }

            public static async Task<TwoNodeScenario> StartAsync(bool subjectHasSigningKey)
            {
                var chainId = new BigInteger(420420_600 + Environment.TickCount % 1000);
                var genesisOwnerKey = EthECKey.GenerateKey();
                var sequencerKey = EthECKey.GenerateKey();

                var subjectNodeKey = EthECKey.GenerateKey();
                var siblingNodeKey = EthECKey.GenerateKey();
                var ports = ReserveFreeLoopbackPorts(2);
                var subjectPort = ports[0];
                var siblingPort = ports[1];
                var siblingEnode = EnodeOf(siblingNodeKey, siblingPort);

                var subjectConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, subjectHasSigningKey,
                    subjectNodeKey, subjectPort, trustedPeers: new[] { siblingEnode });

                var siblingConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: true,
                    siblingNodeKey, siblingPort, trustedPeers: Array.Empty<string>());

                var loggerFactory = NullLoggerFactory.Instance;
                var lifetime = new CancellationTokenSource();

                var sibling = await ComposedAppChainNode.ComposeAsync(siblingConfig, loggerFactory, lifetime.Token);
                var subject = await ComposedAppChainNode.ComposeAsync(subjectConfig, loggerFactory, lifetime.Token);

                return new TwoNodeScenario(subject, sibling);
            }

            public async ValueTask DisposeAsync()
            {
                await Subject.DisposeAsync();
                await Sibling.DisposeAsync();
            }
        }

        private sealed class HaTwoNodeScenario : IAsyncDisposable
        {
            private HaTwoNodeScenario(
                ComposedAppChainNode a, ComposedAppChainNode b, InMemorySequencerArbiter arbiter,
                string aNodeId, string bNodeId)
            {
                A = a;
                B = b;
                Arbiter = arbiter;
                ANodeId = aNodeId;
                BNodeId = bNodeId;
            }

            public ComposedAppChainNode A { get; }

            public ComposedAppChainNode B { get; }

            public InMemorySequencerArbiter Arbiter { get; }

            public string ANodeId { get; }

            public string BNodeId { get; }

            public ArbiterBackedProducerAuthority AAuthority => (ArbiterBackedProducerAuthority)A.ProducerAuthority!;

            public ArbiterBackedProducerAuthority BAuthority => (ArbiterBackedProducerAuthority)B.ProducerAuthority!;

            public static async Task<HaTwoNodeScenario> StartAsync(bool bImports = true)
            {
                var chainId = new BigInteger(420420_700 + Environment.TickCount % 1000);
                var genesisOwnerKey = EthECKey.GenerateKey();
                var sequencerKey = EthECKey.GenerateKey();

                var aNodeKey = EthECKey.GenerateKey();
                var bNodeKey = EthECKey.GenerateKey();
                var ports = ReserveFreeLoopbackPorts(2);
                var aPort = ports[0];
                var bPort = ports[1];
                var aEnode = EnodeOf(aNodeKey, aPort);

                const string aNodeId = "A";
                const string bNodeId = "B";
                var arbiter = new InMemorySequencerArbiter(TimeSpan.FromSeconds(30));
                var aAuthority = new ArbiterBackedProducerAuthority(
                    arbiter, aNodeId, TimeSpan.FromMilliseconds(200), startBackgroundLoop: false);
                var bAuthority = new ArbiterBackedProducerAuthority(
                    arbiter, bNodeId, TimeSpan.FromMilliseconds(200), startBackgroundLoop: false);

                var aConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: true,
                    aNodeKey, aPort, trustedPeers: Array.Empty<string>());
                aConfig.Consensus.NodeIdentity = aNodeId;
                aConfig.Consensus.ProducerAuthorityOverride = aAuthority;

                var bConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: true,
                    bNodeKey, bPort, trustedPeers: bImports ? new[] { aEnode } : Array.Empty<string>());
                bConfig.Consensus.NodeIdentity = bNodeId;
                bConfig.Consensus.ProducerAuthorityOverride = bAuthority;
                if (!bImports) bConfig.Node.Sync.Mode = SyncMode.None;

                var loggerFactory = NullLoggerFactory.Instance;
                var lifetime = new CancellationTokenSource();

                var a = await ComposedAppChainNode.ComposeAsync(aConfig, loggerFactory, lifetime.Token);
                var b = await ComposedAppChainNode.ComposeAsync(bConfig, loggerFactory, lifetime.Token);

                await aAuthority.PollOnceAsync();

                return new HaTwoNodeScenario(a, b, arbiter, aNodeId, bNodeId);
            }

            public async ValueTask DisposeAsync()
            {
                await A.DisposeAsync();
                await B.DisposeAsync();
            }
        }

        private sealed class ComposedAppChainNode : IAsyncDisposable
        {
            private readonly AppChainComposedNode _composed;
            private readonly bool _signRecoverableBeforeCompose;

            private ComposedAppChainNode(AppChainComposedNode composed, bool signRecoverableBeforeCompose)
            {
                _composed = composed;
                _signRecoverableBeforeCompose = signRecoverableBeforeCompose;
            }

            public IChainStoreBundle Bundle => _composed.Bundle;

            public ISequencer Sequencer => _composed.Sequencer;

            public bool Imports => _composed.Imports;

            public bool Produces => _composed.Produces;

            public IProducerAuthority? ProducerAuthority => _composed.ProducerAuthority;

            public Nethereum.AppChain.AppChain AppChain => _composed.AppChain;

            public static async Task<ComposedAppChainNode> ComposeAsync(
                AppChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken ct)
            {
                config.Mud.DeployWorld = false;

                var signRecoverableBeforeCompose = EthECKey.SignRecoverable;
                var composed = await AppChainComposition.ComposeAsync(config, loggerFactory, ct);
                return new ComposedAppChainNode(composed, signRecoverableBeforeCompose);
            }

            public async ValueTask DisposeAsync()
            {
                await _composed.DisposeAsync();
                EthECKey.SignRecoverable = _signRecoverableBeforeCompose;
            }
        }
    }
}
