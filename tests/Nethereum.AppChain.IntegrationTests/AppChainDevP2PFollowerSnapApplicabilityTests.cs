using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainDevP2PFollowerSnapApplicabilityTests
    {
        private static readonly TimeSpan ProgressTimeout = TimeSpan.FromSeconds(45);

        [Fact]
        public async Task Given_AFreshlyStartedSameGenesisCliqueCluster_When_ASignerImportsItsSiblingsBlocks_Then_TheProductionFollowerMakesProgressWithoutASnapPivot()
        {
            var log = new CapturingLoggerProvider();
            var loggerFactory = LoggerFactory.Create(b => { b.AddProvider(log); b.SetMinimumLevel(LogLevel.Information); });

            var chainId = new BigInteger(420421_100 + Environment.TickCount % 1000);
            var genesisOwnerKey = EthECKey.GenerateKey();
            var sharedSequencerKey = EthECKey.GenerateKey();
            var sealerKey = EthECKey.GenerateKey();
            var importerKey = EthECKey.GenerateKey();
            var initialSigners = new[] { sealerKey.GetPublicAddress(), importerKey.GetPublicAddress() };

            var sealerNodeKey = EthECKey.GenerateKey();
            var importerNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);
            var sealerEnode = EnodeOf(sealerNodeKey, ports[0]);
            var importerEnode = EnodeOf(importerNodeKey, ports[1]);

            var sealerConfig = BuildCliqueConfig(
                chainId, genesisOwnerKey, sharedSequencerKey, sealerKey, initialSigners,
                sealerNodeKey, ports[0], trustedPeers: new[] { importerEnode });

            var importerConfig = BuildCliqueConfig(
                chainId, genesisOwnerKey, sharedSequencerKey, importerKey, initialSigners,
                importerNodeKey, ports[1], trustedPeers: new[] { sealerEnode });

            var sealer = await AppChainComposition.ComposeAsync(sealerConfig, loggerFactory, CancellationToken.None);
            var importer = await AppChainComposition.ComposeAsync(importerConfig, loggerFactory, CancellationToken.None);
            try
            {
                var madeProgress = await WaitUntilAsync(
                    () => importer.Bundle.Metadata.GetLastBlock() >= 1,
                    ProgressTimeout);

                Assert.True(madeProgress,
                    $"the production follower never executed a block from its sibling; lastBlock={importer.Bundle.Metadata.GetLastBlock()}. " +
                    "A same-genesis cluster's peer tip never exceeds the pivot trail distance, so if the follower is still " +
                    "trying to resolve a snap pivot before forward-executing, it stalls forever.");

                Assert.Contains(log.Messages, m => m.Contains("DevP2P snap bootstrap: skipped"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("snap.bootstrap.entry"));
            }
            finally
            {
                await importer.DisposeAsync();
                await sealer.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_APeerMoreThanSixtyFourBlocksAhead_When_AFollowerStarts_Then_ItStillResolvesASnapPivotAsItDoesToday()
        {
            var log = new CapturingLoggerProvider();
            var loggerFactory = LoggerFactory.Create(b => { b.AddProvider(log); b.SetMinimumLevel(LogLevel.Information); });

            var chainId = new BigInteger(420421_200 + Environment.TickCount % 1000);
            var genesisOwnerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();

            var producerNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);
            var producerEnode = EnodeOf(producerNodeKey, ports[0]);

            var producerConfig = BuildSingleSequencerConfig(
                chainId, genesisOwnerKey, sequencerKey, hasSigningKey: true,
                producerNodeKey, ports[0], trustedPeers: Array.Empty<string>());

            var producer = await AppChainComposition.ComposeAsync(producerConfig, loggerFactory, CancellationToken.None);
            try
            {
                var blocksAheadOfGenesis = SnapSyncOrchestratorPivotTrailDistance() + 1;
                for (ulong i = 0; i < blocksAheadOfGenesis; i++)
                {
                    var sealedBlock = await producer.Sequencer.ProduceBlockAsync();
                    Assert.NotNull(sealedBlock);
                }

                var producerHeight = await producer.Bundle.Blocks.GetHeightAsync();
                Assert.True(producerHeight > SnapSyncOrchestratorPivotTrailDistance(),
                    $"the producer needs to be strictly more than the pivot trail distance ahead of genesis for this test to mean anything; height={producerHeight}");

                var followerConfig = BuildSingleSequencerConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: false,
                    followerNodeKey, ports[1], trustedPeers: new[] { producerEnode });

                var follower = await AppChainComposition.ComposeAsync(followerConfig, loggerFactory, CancellationToken.None);
                try
                {
                    var resolvedAPivot = await WaitUntilAsync(
                        () => log.Messages.Any(m => m.Contains("snap.bootstrap.entry")),
                        ProgressTimeout);

                    Assert.True(resolvedAPivot,
                        "a follower whose peer is more than the pivot trail distance ahead of genesis must still " +
                        "enter snap bootstrap and resolve a pivot, exactly as it did before this fix");
                    Assert.DoesNotContain(log.Messages, m => m.Contains("DevP2P snap bootstrap: skipped"));
                }
                finally
                {
                    await follower.DisposeAsync();
                }
            }
            finally
            {
                await producer.DisposeAsync();
            }
        }

        private static ulong SnapSyncOrchestratorPivotTrailDistance() =>
            Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapSyncOrchestrator.PivotTrailDistance;

        private static AppChainServerConfig BuildCliqueConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, EthECKey cliqueSignerKey,
            string[] initialSigners, EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "SnapApplicabilityCliqueE2E" };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = cliqueSignerKey.GetPrivateKey();
            config.Consensus.Clique.InitialSigners = initialSigners;
            config.Consensus.Clique.PeriodSeconds = 1;
            config.Consensus.Clique.EpochLength = 30000;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 500;
            ApplyNetwork(config, nodeKey, listenPort, trustedPeers);
            return config;
        }

        private static AppChainServerConfig BuildSingleSequencerConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, bool hasSigningKey,
            EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "SnapApplicabilitySingleSequencerE2E" };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            if (hasSigningKey)
                config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            else
                config.Consensus.Sequencer.Address = sequencerKey.GetPublicAddress();
            ApplyNetwork(config, nodeKey, listenPort, trustedPeers);
            return config;
        }

        private static void ApplyNetwork(
            AppChainServerConfig config, EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
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
            config.Mud.DeployWorld = false;
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

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(100).ConfigureAwait(false);
            }
            return condition();
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<string> _messages = new();

            public IReadOnlyCollection<string> Messages => _messages;

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

            public void Dispose() { }

            private sealed class CapturingLogger : ILogger
            {
                private readonly ConcurrentQueue<string> _messages;

                public CapturingLogger(ConcurrentQueue<string> messages) { _messages = messages; }

                public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel, EventId eventId, TState state, Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    _messages.Enqueue(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
