using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using HostedNode = Nethereum.ChainNode.Hosting.ChainNode;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeCompositionTests
    {
        private const ulong TestChainId = 909090;

        private static ChainNodeConfig InMemoryConfig()
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.Serve = false;
            config.Network.ListenPort = 0;
            config.Network.BindAddress = System.Net.IPAddress.Loopback;
            config.Sync.Mode = SyncMode.None;
            return config;
        }

        [Fact]
        public async Task Given_AChainDefinition_When_TheNodeStarts_Then_StorageGenesisProfileTxPoolServeSyncAndBridgeAreComposedInThatOrder()
        {
            var definition = new RecordingDefinition();

            await using var node = await HostedNode.StartAsync(definition, InMemoryConfig(), loggerFactory: null);

            Assert.Equal(new[] { "genesis", "profile" }, definition.Calls);
            Assert.NotNull(node.Bundle);
            Assert.NotNull(node.Mempool);
            Assert.True(
                definition.BundleAtGenesis is not null && ReferenceEquals(definition.BundleAtGenesis, node.Bundle),
                "genesis was written into a different bundle than the node holds");
        }

        [Fact]
        public async Task Given_ADefinitionWhoseGenesisIsNeverWritten_When_TheNodeStarts_Then_ItFailsLoudlyRatherThanAssertingAnEmptyIdentity()
        {
            var definition = new RecordingDefinition { WriteGenesis = false };

            var failure = await Assert.ThrowsAnyAsync<Exception>(
                () => HostedNode.StartAsync(definition, InMemoryConfig(), loggerFactory: null));

            Assert.Contains("genesis", failure.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_ServeDisabled_When_TheNodeStarts_Then_NoListenerIsCreated()
        {
            var config = InMemoryConfig();
            config.Network.Serve = false;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.Null(node.Listener);
        }

        [Fact]
        public async Task Given_ServeEnabled_When_TheNodeStarts_Then_AListenerIsCreatedOnTheConfiguredPort()
        {
            var config = InMemoryConfig();
            config.Network.Serve = true;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.NotNull(node.Listener);
            Assert.True(node.Listener.Port > 0, "an ephemeral port was requested but none was bound");
        }

        [Fact]
        public async Task Given_SyncModeNone_When_TheNodeStarts_Then_NoSyncStackIsCreated()
        {
            var config = InMemoryConfig();
            config.Sync.Mode = SyncMode.None;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.Null(node.Sync);
        }

        [Fact]
        public async Task Given_ASyncMode_When_TheNodeStarts_Then_ASyncStackIsCreated()
        {
            var config = InMemoryConfig();
            config.Sync.Mode = SyncMode.ForwardExecute;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.NotNull(node.Sync);
            Assert.NotNull(node.Sync.Pool);
        }

        [Fact]
        public async Task Given_AnInMemoryConfig_When_TheNodeStarts_Then_ItStillHasAUsableBundle()
        {
            var config = InMemoryConfig();
            config.Storage.InMemory = true;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.NotNull(node.Bundle);
            Assert.NotNull(await node.Bundle.Blocks.GetHashByNumberAsync(0));
        }

        [Fact]
        public async Task Given_ADefinitionSupplyingATip_When_TheNodeStarts_Then_TheSyncStackUsesItRatherThanThePeerHead()
        {
            var supplied = new FixedTipCanonicalSource(42, new byte[32], new byte[32]);
            var config = InMemoryConfig();
            config.Sync.Mode = SyncMode.ForwardExecute;

            await using var node = await HostedNode.StartAsync(
                new RecordingDefinition { Tip = supplied }, config, loggerFactory: null);

            Assert.Same(supplied, node.Sync.Tip);
        }

        [Fact]
        public async Task Given_ADefinitionSupplyingNoTip_When_TheNodeStarts_Then_TheSyncStackFallsBackToThePeerHead()
        {
            var config = InMemoryConfig();
            config.Sync.Mode = SyncMode.ForwardExecute;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.IsType<PeerHeadCanonicalSource>(node.Sync.Tip);
        }

        [Fact]
        public async Task Given_TheSameConfigAndDefinition_When_TwoNodesAreComposed_Then_TheyAgreeOnGenesisAndNetworkId()
        {
            await using var first = await HostedNode.StartAsync(new RecordingDefinition(), InMemoryConfig(), loggerFactory: null);
            await using var second = await HostedNode.StartAsync(new RecordingDefinition(), InMemoryConfig(), loggerFactory: null);

            Assert.Equal(first.Profile.NetworkId, second.Profile.NetworkId);
            Assert.True(ByteUtil.AreEqual(first.Profile.GenesisHash, second.Profile.GenesisHash),
                "two nodes of the same chain disagree on genesis");
        }

        [Fact]
        public async Task Given_APluggableTxPoolImplementation_When_TheNodeIsComposed_Then_ThatImplementationIsUsedRatherThanTheDefault()
        {
            var supplied = new TxPool();
            var config = InMemoryConfig();
            config.Mempool.PoolFactory = _ => supplied;

            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.Same(supplied, node.Mempool.TxPool);
        }

        [Fact]
        public async Task Given_NoPoolFactory_When_TheNodeIsComposed_Then_ItStillHasAWorkingPool()
        {
            await using var node = await HostedNode.StartAsync(new RecordingDefinition(), InMemoryConfig(), loggerFactory: null);

            Assert.NotNull(node.Mempool.TxPool);
            Assert.NotNull(node.Mempool.Relay);
        }

        private sealed class RecordingDefinition : IChainDefinition
        {
            private readonly List<string> _calls = new List<string>();

            public IReadOnlyList<string> Calls => _calls;

            public bool WriteGenesis { get; set; } = true;

            public ICanonicalStateRootSource Tip { get; set; }

            public IChainStoreBundle BundleAtGenesis { get; private set; }

            public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
            {
                _calls.Add("genesis");
                BundleAtGenesis = bundle;
                if (WriteGenesis) await TestGenesis.WriteAsync(bundle);
            }

            public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
            {
                _calls.Add("profile");

                var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0);
                if (genesisHash == null)
                    throw new InvalidOperationException("no genesis block; a node cannot assert an identity without one");

                return new TestProfile(TestChainId, genesisHash);
            }

            public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => Tip;
        }

        private sealed class TestProfile : IChainProfile
        {
            public TestProfile(ulong networkId, byte[] genesisHash)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
                (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                new ChainPeerHandshakeWorker(
                    GenesisHash, NetworkId, ForkThresholds, ourHead: null, logger: null, advertiseSnap2: advertiseSnap2);
        }
    }
}
