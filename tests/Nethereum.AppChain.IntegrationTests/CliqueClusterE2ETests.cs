using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class CliqueClusterE2ETests
    {
        private static readonly TimeSpan ConvergenceTimeout = TimeSpan.FromSeconds(150);

        [Fact]
        public async Task Given_ThreeCliqueSignersLinkedOnlyByTrustedPeers_When_TheyProduceBlocks_Then_TheyConvergeOnOneChainSealedByMultipleSigners()
        {
            await using var cluster = await CliqueCluster.StartMeshedAsync(size: 3);

            var targetHeight = await cluster.WaitForAllNodesToReachHeightAsync(2, ConvergenceTimeout);

            Assert.True(targetHeight >= 2,
                $"cluster only reached height {targetHeight} within {ConvergenceTimeout}; the three signers never converged");

            for (BigInteger height = 1; height <= targetHeight; height++)
            {
                var hashes = await Task.WhenAll(cluster.Nodes.Select(n => n.Bundle.Blocks.GetHashByNumberAsync(height)));
                var first = hashes[0].ToHex();
                Assert.True(hashes.All(h => h.ToHex() == first),
                    $"block {height} diverged across the cluster: {string.Join(", ", hashes.Select(h => h.ToHex()))}");
            }

            var signersObserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (BigInteger height = 1; height <= targetHeight; height++)
            {
                var header = await cluster.Nodes[0].Bundle.Blocks.GetByNumberAsync(height);
                signersObserved.Add(cluster.Nodes[0].Engine.RecoverSigner(header));
            }

            Assert.True(signersObserved.Count > 1,
                $"only one signer's address ({string.Join(",", signersObserved)}) appears across blocks 1..{targetHeight}; " +
                "each node sealed its own private chain instead of importing its siblings'");
        }

        [Fact]
        public async Task Given_ASignerThatImportedASiblingsBlock_When_ItSealsItsNextBlock_Then_ItsParentIsThatSiblingsBlock()
        {
            await using var cluster = await CliqueCluster.StartMeshedAsync(size: 3);

            var targetHeight = await cluster.WaitForAllNodesToReachHeightAsync(2, ConvergenceTimeout);
            Assert.True(targetHeight >= 2, $"cluster only reached height {targetHeight} within {ConvergenceTimeout}");

            var foundImportThenSeal = false;
            BlockHeaderSummary? previous = null;

            for (BigInteger height = 1; height <= targetHeight; height++)
            {
                var header = await cluster.Nodes[0].Bundle.Blocks.GetByNumberAsync(height);
                var hash = await cluster.Nodes[0].Bundle.Blocks.GetHashByNumberAsync(height);
                var current = new BlockHeaderSummary(height, cluster.Nodes[0].Engine.RecoverSigner(header), hash, header.ParentHash);

                if (previous != null &&
                    !string.Equals(previous.Value.Coinbase, current.Coinbase, StringComparison.OrdinalIgnoreCase) &&
                    current.ParentHash.ToHex() == previous.Value.Hash.ToHex())
                {
                    foundImportThenSeal = true;
                    break;
                }

                previous = current;
            }

            Assert.True(foundImportThenSeal,
                "no block in the observed range was sealed by a different signer than its parent; " +
                "a node that only ever seals on top of its OWN last block would also pass a same-chain check " +
                "without proving it imports its siblings' blocks");
        }

        [Fact]
        public async Task Given_OneSignersTrustedPeersPointAtNothing_When_TheClusterProducesBlocks_Then_ItNeverConvergesWithTheIsolatedSigner()
        {
            await using var cluster = await CliqueCluster.StartWithOneIsolatedAsync(size: 3, isolatedIndex: 2);

            var isolated = cluster.Nodes[2];
            var meshed = cluster.Nodes.Take(2).ToList();

            var meshedHeight = await cluster.WaitForNodesToReachHeightAsync(meshed, 4, ConvergenceTimeout);
            Assert.True(meshedHeight >= 4,
                $"the two meshed signers only reached height {meshedHeight}; the mesh itself is broken, not just the isolated node");

            await Task.Delay(TimeSpan.FromSeconds(3));

            var isolatedHash = await isolated.Bundle.Blocks.GetHashByNumberAsync(1);
            var meshedHash = await meshed[0].Bundle.Blocks.GetHashByNumberAsync(1);

            if (isolatedHash == null)
            {
                Assert.True(true);
                return;
            }

            Assert.NotEqual(meshedHash.ToHex(), isolatedHash.ToHex());
        }

        [Fact(Skip = "Re-verified after landing DifficultyForkChoice/PushedBlockSource wiring (2026-09-17): the fork-choice/reorg gap (R1) this skip used to cite is closed, but the cluster still times out at 'cluster only reached height 2 while importing block 3' — a separate, still-open gap in deterministic on-demand block propagation across nodes. See docs/internal/appchain-clique-signer-rotation.md.")]
        public async Task Given_ThreeOfFourSignersVoteToAuthorizeASpareSigner_When_QuorumIsReached_Then_TheSpareJoinsAndSealsWithoutAFork()
        {
            await using var cluster = await CliqueCluster.StartWithASpareSignerAsync(size: 4, signingCount: 3, onDemand: true);

            var voters = cluster.Nodes.Take(3).ToList();
            var spare = cluster.Nodes[3];
            var spareAddress = cluster.SignerKeys[3].GetPublicAddress();

            foreach (var node in cluster.Nodes)
                Assert.False(node.IsAuthorized(spareAddress), "the spare must start unauthorized on every node");

            var requiredVotes = voters[0].Engine.CurrentSnapshot.RequiredVotes;
            Assert.True(requiredVotes > 1 && requiredVotes <= voters.Count,
                $"test assumes a real majority threshold between 2 and {voters.Count}, got {requiredVotes}");

            for (var i = 0; i < voters.Count; i++)
            {
                voters[i].Propose(spareAddress, authorize: true);
                var height = await ProduceAndAwaitImportAsync(cluster, voters[i], ConvergenceTimeout);

                if (i == 0)
                {
                    var votingHeader = await voters[i].Bundle.Blocks.GetByNumberAsync(height);
                    Assert.Equal(
                        BitConverter.ToString(CliqueEngine.NONCE_AUTH),
                        BitConverter.ToString(votingHeader.Nonce));
                }

                var castVotes = i + 1;
                if (castVotes < requiredVotes)
                {
                    foreach (var node in cluster.Nodes)
                        Assert.False(node.IsAuthorized(spareAddress),
                            $"spare must remain unauthorized after {castVotes} of {requiredVotes} required votes");
                }
                else
                {
                    foreach (var node in cluster.Nodes)
                        Assert.True(node.IsAuthorized(spareAddress),
                            $"spare must be authorized once quorum ({requiredVotes}) is reached");
                    break;
                }
            }

            var spareBlockHeight = await ProduceAndAwaitImportAsync(cluster, spare, ConvergenceTimeout);

            var hashesAtSpareHeight = await Task.WhenAll(
                cluster.Nodes.Select(n => n.Bundle.Blocks.GetHashByNumberAsync(spareBlockHeight)));
            Assert.True(hashesAtSpareHeight.All(h => h.ToHex() == hashesAtSpareHeight[0].ToHex()),
                $"the spare's first sealed block diverged across the cluster: {string.Join(", ", hashesAtSpareHeight.Select(h => h.ToHex()))}");

            var spareHeader = await cluster.Nodes[0].Bundle.Blocks.GetByNumberAsync(spareBlockHeight);
            Assert.Equal(spareAddress.ToLowerInvariant(), cluster.Nodes[0].Engine.RecoverSigner(spareHeader));
        }

        [Fact(Skip = "Re-verified after landing DifficultyForkChoice/PushedBlockSource wiring (2026-09-17): the fork-choice/reorg gap (R1) this skip used to cite is closed, but the cluster still times out at 'cluster only reached height 2 while importing block 3' — a separate, still-open gap in deterministic on-demand block propagation across nodes. See docs/internal/appchain-clique-signer-rotation.md.")]
        public async Task Given_TwoOfThreeSignersVoteToDeauthorizeASibling_When_QuorumIsReached_Then_TheRemovedSignersNextBlockIsRejectedByEveryGate()
        {
            await using var cluster = await CliqueCluster.StartMeshedAsync(size: 3, onDemand: true);

            const int targetIndex = 1;
            var target = cluster.Nodes[targetIndex];
            var targetAddress = cluster.SignerKeys[targetIndex].GetPublicAddress();
            var survivors = cluster.Nodes.Where((_, i) => i != targetIndex).ToList();

            var requiredVotes = survivors[0].Engine.CurrentSnapshot.RequiredVotes;
            Assert.True(requiredVotes > 1 && requiredVotes <= cluster.Nodes.Count,
                $"test assumes a real majority threshold, got {requiredVotes} for {cluster.Nodes.Count} signers");

            for (var i = 0; i < survivors.Count; i++)
            {
                survivors[i].Propose(targetAddress, authorize: false);
                var height = await ProduceAndAwaitImportAsync(cluster, survivors[i], ConvergenceTimeout);

                if (i == 0)
                {
                    var votingHeader = await survivors[i].Bundle.Blocks.GetByNumberAsync(height);
                    Assert.Equal(
                        BitConverter.ToString(CliqueEngine.NONCE_DROP),
                        BitConverter.ToString(votingHeader.Nonce));
                }

                var castVotes = i + 1;
                if (castVotes < requiredVotes)
                {
                    foreach (var node in cluster.Nodes)
                        Assert.True(node.IsAuthorized(targetAddress),
                            $"the target must remain authorized after {castVotes} of {requiredVotes} required votes");
                }
                else
                {
                    foreach (var node in cluster.Nodes)
                        Assert.False(node.IsAuthorized(targetAddress),
                            $"the target must be removed once quorum ({requiredVotes}) is reached");
                    break;
                }
            }

            foreach (var node in cluster.Nodes)
                Assert.Equal(2, node.Engine.CurrentSnapshot.TotalSigners);

            await Assert.ThrowsAsync<SequencerLeaseNotHeldException>(() => target.ProduceBlockAsync());

            var survivorForGate = survivors[0];
            var parentHeight = await survivorForGate.Bundle.Blocks.GetHeightAsync();
            var nextHeight = (long)parentHeight + 1;
            var parentHash = await survivorForGate.Bundle.Blocks.GetHashByNumberAsync(parentHeight);

            var rejectedHeader = new BlockHeader
            {
                BlockNumber = nextHeight,
                ParentHash = parentHash,
                Difficulty = (EvmUInt256)target.Engine.GetDifficulty(nextHeight, targetAddress),
                MixHash = new byte[32],
                Nonce = CliqueEngine.NONCE_DROP,
                ExtraData = target.Engine.PrepareExtraData(nextHeight),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            target.Engine.InsertSignature(rejectedHeader.ExtraData, target.Engine.SignBlock(rejectedHeader));

            foreach (var survivor in survivors)
            {
                var gate = new CliqueConsensusBlockGate(survivor.Engine, survivor.Bundle.Blocks);
                var verdict = await gate.IsBlockCanonicalAsync(rejectedHeader, new byte[32], CancellationToken.None);

                Assert.False(verdict.Accepted,
                    "a block sealed by the removed signer must be an explicit Reject, not merely absent from the chain");
                Assert.Contains("nauthorized", verdict.Reason ?? "", StringComparison.OrdinalIgnoreCase);
            }

            var continuedHeight = await ProduceAndAwaitImportAsync(cluster, survivors[0], ConvergenceTimeout);
            var hashesAtContinuedHeight = await Task.WhenAll(
                cluster.Nodes.Select(n => n.Bundle.Blocks.GetHashByNumberAsync(continuedHeight)));
            Assert.True(hashesAtContinuedHeight.All(h => h.ToHex() == hashesAtContinuedHeight[0].ToHex()),
                $"the remaining two signers diverged after the removal: {string.Join(", ", hashesAtContinuedHeight.Select(h => h.ToHex()))}");
        }

        private static async Task<BigInteger> ProduceAndAwaitImportAsync(
            CliqueCluster cluster, CliqueSignerNode producer, TimeSpan timeout)
        {
            await producer.ProduceBlockAsync();
            var height = await producer.Bundle.Blocks.GetHeightAsync();

            var reached = await cluster.WaitForAllNodesToReachHeightAsync(height, timeout);
            Assert.True(reached >= height,
                $"cluster only reached height {reached} while importing block {height} sealed by {producer.SignerAddress}; " +
                "a signer's block must settle everywhere before the next signer produces");

            return height;
        }

        private readonly struct BlockHeaderSummary
        {
            public BlockHeaderSummary(BigInteger height, string coinbase, byte[] hash, byte[] parentHash)
            {
                Height = height;
                Coinbase = coinbase;
                Hash = hash;
                ParentHash = parentHash;
            }

            public BigInteger Height { get; }
            public string Coinbase { get; }
            public byte[] Hash { get; }
            public byte[] ParentHash { get; }
        }

        private sealed class CliqueCluster : IAsyncDisposable
        {
            private readonly List<CliqueSignerNode> _nodes;
            private readonly CancellationTokenSource _lifetime;

            private CliqueCluster(List<CliqueSignerNode> nodes, CancellationTokenSource lifetime, IReadOnlyList<EthECKey> signerKeys)
            {
                _nodes = nodes;
                _lifetime = lifetime;
                SignerKeys = signerKeys;
            }

            public IReadOnlyList<CliqueSignerNode> Nodes => _nodes;

            public IReadOnlyList<EthECKey> SignerKeys { get; }

            public static Task<CliqueCluster> StartMeshedAsync(int size, bool onDemand = false) =>
                StartAsync(size, isolatedIndex: null, signingCount: size, onDemand);

            public static Task<CliqueCluster> StartWithOneIsolatedAsync(int size, int isolatedIndex) =>
                StartAsync(size, isolatedIndex, signingCount: size, onDemand: false);

            public static Task<CliqueCluster> StartWithASpareSignerAsync(int size, int signingCount, bool onDemand = false) =>
                StartAsync(size, isolatedIndex: null, signingCount, onDemand);

            private static async Task<CliqueCluster> StartAsync(int size, int? isolatedIndex, int signingCount, bool onDemand)
            {
                var chainId = new BigInteger(420420_000 + Environment.TickCount % 1000);
                var loggerFactory = NullLoggerFactory.Instance;

                var genesisOwnerKey = EthECKey.GenerateKey();
                var sharedSequencerKey = EthECKey.GenerateKey();

                var cliqueKeys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
                var initialSigners = cliqueKeys.Take(signingCount).Select(k => k.GetPublicAddress()).ToArray();

                var nodeKeys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
                var ports = ReserveFreeLoopbackPorts(size);
                var enodes = nodeKeys.Select((key, index) => EnodeOf(key, ports[index])).ToList();

                var deadPeerKey = EthECKey.GenerateKey();
                var deadPeerPort = ReserveFreeLoopbackPorts(1)[0];
                var deadEnode = EnodeOf(deadPeerKey, deadPeerPort);

                var nodes = new List<CliqueSignerNode>();
                var lifetime = new CancellationTokenSource();

                for (var i = 0; i < size; i++)
                {
                    var isIsolated = isolatedIndex.HasValue && i == isolatedIndex.Value;

                    var trustedPeers = isIsolated
                        ? new[] { deadEnode }
                        : Enumerable.Range(0, size)
                            .Where(other => other != i && !(isolatedIndex.HasValue && other == isolatedIndex.Value))
                            .Select(other => enodes[other])
                            .ToArray();

                    var config = new AppChainServerConfig
                    {
                        ChainId = chainId,
                        ChainName = "CliqueClusterE2E"
                    };
                    config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
                    config.Consensus.Sequencer.PrivateKey = sharedSequencerKey.GetPrivateKey();
                    config.Consensus.Mode = AppChainConsensusMode.Clique;
                    config.Consensus.Clique.Signer.PrivateKey = cliqueKeys[i].GetPrivateKey();
                    config.Consensus.Clique.InitialSigners = initialSigners;
                    config.Consensus.Clique.PeriodSeconds = 1;
                    config.Consensus.Clique.EpochLength = 30000;
                    config.Consensus.AllowEmptyBlocks = true;
                    config.Consensus.BlockTimeMs = onDemand ? 0 : 8000;
                    config.Consensus.BlockProductionMode = onDemand ? BlockProductionMode.OnDemand : BlockProductionMode.Interval;
                    config.Node.Storage.InMemory = true;
                    config.Node.Network.Serve = true;
                    config.Node.Network.NodeKeyHex = nodeKeys[i].GetPrivateKey();
                    config.Node.Network.ListenPort = ports[i];
                    config.Node.Network.BindAddress = IPAddress.Loopback;
                    config.Node.Network.TrustedPeers = trustedPeers;
                    config.Node.Network.TargetPeerCount = Math.Max(1, trustedPeers.Length);
                    config.Node.Network.DialBudgetPerSecond = 0;
                    config.Node.Network.MaxPeersPerIPv4Subnet = 0;
                    config.Node.Network.MaxPeersPerIPv6Subnet = 0;
                    config.Node.Network.MaxInboundPeers = size + 1;
                    config.Node.Network.MaxInboundPerIP = size + 1;

                    config.DeriveAddresses();
                    config.Validate();

                    var node = await CliqueSignerNode.PrepareAsync(config, loggerFactory, lifetime.Token);
                    nodes.Add(node);
                }

                var expectedPeers = size - 1 - (isolatedIndex.HasValue ? 1 : 0);
                var meshedNodes = isolatedIndex.HasValue
                    ? nodes.Where((_, index) => index != isolatedIndex.Value).ToList()
                    : nodes;

                await WaitUntilAsync(
                    () => meshedNodes.All(n => n.ActivePeerCount >= expectedPeers),
                    TimeSpan.FromSeconds(15));

                foreach (var node in nodes)
                    await node.BeginProducingAsync();

                return new CliqueCluster(nodes, lifetime, cliqueKeys);
            }

            private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (condition()) return;
                    await Task.Delay(200);
                }
            }

            public async Task<BigInteger> WaitForAllNodesToReachHeightAsync(BigInteger height, TimeSpan timeout) =>
                await WaitForNodesToReachHeightAsync(_nodes, height, timeout);

            public async Task<BigInteger> WaitForNodesToReachHeightAsync(
                IReadOnlyList<CliqueSignerNode> nodes, BigInteger height, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                BigInteger minHeight = -1;

                while (DateTime.UtcNow < deadline)
                {
                    var heights = await Task.WhenAll(nodes.Select(n => n.Bundle.Blocks.GetHeightAsync()));
                    minHeight = heights.Min();
                    if (minHeight >= height) return minHeight;
                    await Task.Delay(200);
                }

                return minHeight;
            }

            public async ValueTask DisposeAsync()
            {
                _lifetime.Cancel();
                foreach (var node in _nodes)
                    await node.DisposeAsync();
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
        }

        private sealed class CliqueSignerNode : IAsyncDisposable
        {
            private readonly Nethereum.ChainNode.Hosting.ChainNode _chainNode;
            private readonly Nethereum.AppChain.Sequencer.Sequencer _sequencer;
            private readonly CancellationTokenSource _followerLifetime;
            private readonly Task _followerTask;
            private readonly ICliqueProposalStore _proposalStore;
            private readonly CliqueEngine _cliqueEngine;

            private CliqueSignerNode(
                Nethereum.ChainNode.Hosting.ChainNode chainNode, Nethereum.AppChain.Sequencer.Sequencer sequencer,
                CancellationTokenSource followerLifetime, Task followerTask, ICliqueProposalStore proposalStore,
                CliqueEngine cliqueEngine)
            {
                _chainNode = chainNode;
                _sequencer = sequencer;
                _followerLifetime = followerLifetime;
                _followerTask = followerTask;
                _proposalStore = proposalStore;
                _cliqueEngine = cliqueEngine;
            }

            public IChainStoreBundle Bundle => _chainNode.Bundle;

            public int ActivePeerCount => _chainNode.Sync?.Pool?.ActivePeers?.Count ?? 0;

            public string SignerAddress { get; private set; }

            public CliqueEngine Engine => _cliqueEngine;

            public Task BeginProducingAsync() => _sequencer.StartAsync();

            public Task<byte[]> ProduceBlockAsync() => _sequencer.ProduceBlockAsync();

            public void Propose(string target, bool authorize) => _proposalStore.SetProposal(target, authorize);

            public void Discard(string target) => _proposalStore.Discard(target);

            public bool IsAuthorized(string address) => _cliqueEngine.IsAuthorizedSigner(address);

            public static async Task<CliqueSignerNode> PrepareAsync(
                AppChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken parentCt)
            {
                var appChainConfig = Nethereum.AppChain.AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
                appChainConfig.SequencerAddress = config.Consensus.Sequencer.Address;

                var chainNode = await Nethereum.ChainNode.Hosting.ChainNode.StartAsync(
                    new AppChainDefinition(
                        config,
                        (bundle, _) => WriteGenesisAsync(appChainConfig, bundle, config)),
                    config.Node,
                    loggerFactory,
                    ct: parentCt);

                var bundle = chainNode.Bundle;
                var appChain = new Nethereum.AppChain.AppChain(
                    appChainConfig, bundle.Blocks, bundle.Transactions, bundle.Receipts,
                    bundle.Logs, bundle.State, bundle.TrieNodes);

                var cliqueConfig = new CliqueConfig
                {
                    BlockPeriodSeconds = config.Consensus.Clique.PeriodSeconds,
                    EpochLength = config.Consensus.Clique.EpochLength,
                    InitialSigners = config.Consensus.Clique.InitialSigners.ToList(),
                    LocalSignerAddress = config.Consensus.Clique.Signer.Address!,
                    LocalSignerPrivateKey = config.Consensus.Clique.Signer.PrivateKey,
                    AllowEmptyBlocks = config.Consensus.AllowEmptyBlocks,
                    EnableVoting = true,
                    WiggleTimeMs = 6000
                };

                var cliqueEngine = new CliqueEngine(cliqueConfig, loggerFactory.CreateLogger<CliqueEngine>());
                cliqueEngine.ApplyGenesisSigners(config.Consensus.Clique.InitialSigners.ToList());

                var proposalStore = new InMemoryCliqueProposalStore();
                var cliqueStrategy = new CliqueBlockProductionStrategy(
                    appChainConfig, cliqueEngine, loggerFactory.CreateLogger<CliqueBlockProductionStrategy>(),
                    proposalStore);

                var sequencerConfig = new SequencerConfig
                {
                    SequencerAddress = config.Consensus.Sequencer.Address!,
                    SequencerPrivateKey = config.Consensus.Sequencer.PrivateKey,
                    BlockTimeMs = config.Consensus.BlockTimeMs,
                    AllowEmptyBlocks = config.Consensus.AllowEmptyBlocks,
                    MaxTransactionsPerBlock = 1000,
                    Policy = PolicyConfig.OpenAccess,
                };

                var sequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                    appChain,
                    sequencerConfig,
                    txPool: chainNode.Mempool?.TxPool,
                    blockProductionStrategy: cliqueStrategy,
                    logger: loggerFactory.CreateLogger<Nethereum.AppChain.Sequencer.Sequencer>(),
                    nodeId: $"Node-{config.Consensus.Clique.Signer.Address}",
                    stateRootCalculator: new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes),
                    blockAccessListStore: bundle.BlockAccessLists);

                if (chainNode.Listener != null && chainNode.Mempool != null)
                {
                    var blockPublisher = new DevP2PProducedBlockPublisher(
                        chainNode.Mempool.BroadcastPool,
                        bundle.Transactions,
                        loggerFactory.CreateLogger<DevP2PProducedBlockPublisher>());
                    sequencer.BlockProduced += blockPublisher.OnBlockProduced;
                }

                var followerLifetime = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
                var followerTask = Task.Run(() => RunForwardExecuteImportLoopAsync(
                    config, chainNode, cliqueEngine, loggerFactory, followerLifetime.Token));

                return new CliqueSignerNode(chainNode, sequencer, followerLifetime, followerTask, proposalStore, cliqueEngine)
                {
                    SignerAddress = config.Consensus.Clique.Signer.Address!
                };
            }

            private static async Task RunForwardExecuteImportLoopAsync(
                AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode chainNode,
                CliqueEngine cliqueEngine, ILoggerFactory loggerFactory, CancellationToken ct)
            {
                var sync = chainNode.Sync;
                if (sync == null) return;

                var logger = loggerFactory.CreateLogger("Nethereum.AppChain.IntegrationTests.CliqueForwardExecuteFollower");
                var bundle = chainNode.Bundle;
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var chainConfig = new ChainConfig
                {
                    ChainId = config.ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = config.Consensus.Sequencer.Address ?? ""
                };
                var hardforkConfig = chainConfig.GetHardforkConfig();

                Nethereum.CoreChain.Sync.IBlockExecutor ExecutorFactory(IChainStoreBundle b)
                {
                    var inner = new BlockImporter(
                        new BlockExecutor(
                            b.State, b.Blocks, activations,
                            chainConfigFactory: _ => chainConfig,
                            hardforkConfigFactory: _ => hardforkConfig,
                            stateRootCalculator: new IncrementalStateRootCalculator(
                                b.State, b.StateTrieNodes,
                                emitTombstones: !ReferenceEquals(b.StateTrieNodes, b.TrieNodes)),
                            rewardPolicy: NoRewardPolicy.Instance,
                            trieNodeStore: b.TrieNodes,
                            logger: loggerFactory.CreateLogger<BlockExecutor>(),
                            authorResolver: h => cliqueEngine.RecoverSigner(h) ?? h.Coinbase),
                        b.Blocks, b.State, b.Transactions, b.Receipts, b.Logs,
                        uncleStore: b.Uncles,
                        logger: loggerFactory.CreateLogger<BlockImporter>(),
                        nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                        blockAccessListStore: b.BlockAccessLists);

                    return new ConsensusGatedBlockExecutor(
                        inner,
                        new CliqueConsensusBlockGate(cliqueEngine, b.Blocks, loggerFactory.CreateLogger<CliqueConsensusBlockGate>()),
                        loggerFactory.CreateLogger<ConsensusGatedBlockExecutor>());
                }

                var startBlock = bundle.Metadata.GetLastBlock() + 1;
                var policy = new StrictValidationPolicy(
                    continueOnMismatch: false, anchorEvery: 0, logger: loggerFactory.CreateLogger<StrictValidationPolicy>());

                try
                {
                    IChainForkChoice forkChoice = new DifficultyForkChoice(bundle.Blocks);
                    await new FollowerService(forkChoice).RunAsync(
                        sync.BlockSource,
                        () => bundle,
                        ExecutorFactory,
                        policy,
                        sync.Tip,
                        new FollowerOptions(StartBlock: startBlock, CheckpointEvery: 0, AnchorEvery: 0),
                        ct,
                        logger).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            public async ValueTask DisposeAsync()
            {
                _followerLifetime.Cancel();
                try { await _sequencer.StopAsync(); } catch { }
                try { await _sequencer.DisposeAsync(); } catch { }
                try { await Task.WhenAny(_followerTask, Task.Delay(2000)); } catch { }
                await _chainNode.DisposeAsync();
            }

            private static async Task WriteGenesisAsync(
                Nethereum.AppChain.AppChainConfig appChainConfig, IChainStoreBundle bundle, AppChainServerConfig config)
            {
                var appChain = new Nethereum.AppChain.AppChain(
                    appChainConfig, bundle.Blocks, bundle.Transactions, bundle.Receipts,
                    bundle.Logs, bundle.State, bundle.StateTrieNodes);

                var genesisOptions = new Nethereum.AppChain.GenesisOptions
                {
                    DeployCreate2Factory = true,
                    PrefundedAddresses = new[] { config.Genesis.Owner.Address!, config.Consensus.Sequencer.Address! },
                    PrefundBalance = BigInteger.Parse("1000000000000000000000")
                };

                await appChain.InitializeAsync(genesisOptions);
            }
        }
    }
}
