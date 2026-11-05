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
    public class CliqueSiblingBlockConvergenceTests
    {
        private static readonly TimeSpan ConvergenceTimeout = TimeSpan.FromSeconds(60);

        [Fact]
        public async Task Given_TwoSignersThatEachSealedBlockOne_When_TheyConnect_Then_BothConvergeOnTheHeavierChainAndNeitherFollowerStops()
        {
            var log = new CapturingLoggerProvider();
            var loggerFactory = LoggerFactory.Create(b => { b.AddProvider(log); b.SetMinimumLevel(LogLevel.Information); });

            var chainId = new BigInteger(420422_100 + Environment.TickCount % 1000);
            var genesisOwnerKey = EthECKey.GenerateKey();
            var sharedSequencerKey = EthECKey.GenerateKey();
            var firstKey = EthECKey.GenerateKey();
            var secondKey = EthECKey.GenerateKey();
            var initialSigners = new[] { firstKey.GetPublicAddress(), secondKey.GetPublicAddress() };

            var firstNodeKey = EthECKey.GenerateKey();
            var secondNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(3);
            var unreachable = EnodeOf(EthECKey.GenerateKey(), ports[2]);

            var first = await AppChainComposition.ComposeAsync(
                BuildConfig(chainId, genesisOwnerKey, sharedSequencerKey, firstKey, initialSigners, firstNodeKey, ports[0], unreachable),
                loggerFactory, CancellationToken.None);
            var second = await AppChainComposition.ComposeAsync(
                BuildConfig(chainId, genesisOwnerKey, sharedSequencerKey, secondKey, initialSigners, secondNodeKey, ports[1], unreachable),
                loggerFactory, CancellationToken.None);
            try
            {
                await first.Sequencer!.ProduceBlockAsync();
                await second.Sequencer!.ProduceBlockAsync();

                var firstOwn = await first.Bundle.Blocks.GetHashByNumberAsync(1);
                var secondOwn = await second.Bundle.Blocks.GetHashByNumberAsync(1);
                Assert.NotEqual(firstOwn.ToHex(), secondOwn.ToHex());
                Assert.Equal(1UL, first.Bundle.Metadata.GetLastBlock());
                Assert.Equal(1UL, second.Bundle.Metadata.GetLastBlock());

                first.ChainNode.Sync!.Pool.EnqueueCandidate(EnodeOf(secondNodeKey, ports[1]));
                second.ChainNode.Sync!.Pool.EnqueueCandidate(EnodeOf(firstNodeKey, ports[0]));

                var connected = await WaitUntilAsync(
                    () => Task.FromResult(first.ChainNode.Sync!.Pool.ActivePeers.Count > 0 && second.ChainNode.Sync!.Pool.ActivePeers.Count > 0),
                    ConvergenceTimeout);
                Assert.True(connected, "the two signers never peered");

                AnnounceBlockOne(first);
                AnnounceBlockOne(second);

                var converged = await WaitUntilAsync(async () =>
                {
                    var a = await first.Bundle.Blocks.GetHashByNumberAsync(1);
                    var b = await second.Bundle.Blocks.GetHashByNumberAsync(1);
                    return a != null && b != null && a.ToHex() == b.ToHex();
                }, ConvergenceTimeout);
                Assert.True(converged, "the two signers never agreed on block 1 " + string.Join(" | ", log.Messages.Where(m => !m.Contains("block.exec.timing")).TakeLast(60)));

                var settled = await first.Bundle.Blocks.GetByNumberAsync(1);
                Assert.Equal(new BigInteger(2), (BigInteger)settled.Difficulty);

                var settledHash = await first.Bundle.Blocks.GetHashByNumberAsync(1);
                var loser = settledHash.ToHex() == firstOwn.ToHex() ? second : first;
                var winner = ReferenceEquals(loser, first) ? second : first;
                await loser.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, genesisOwnerKey, nonce: 0));

                var carriedOn = await WaitUntilAsync(async () =>
                {
                    var a = await winner.Bundle.Blocks.GetHashByNumberAsync(2);
                    var b = await loser.Bundle.Blocks.GetHashByNumberAsync(2);
                    return a != null && b != null && a.ToHex() == b.ToHex();
                }, ConvergenceTimeout);
                Assert.True(carriedOn, "the signer that lost block 1 sealed block 2 and the winner never imported it " + string.Join(" | ", log.Messages.Where(m => !m.Contains("Produced by") && !m.Contains("trusted peer keeper")).TakeLast(40)));

                var carried = await winner.Bundle.Blocks.GetByNumberAsync(2);
                Assert.True(carried.GasUsed > 0, "block 2 must carry the transaction");
                foreach (var node in new[] { winner, loser })
                {
                    var full = await new Nethereum.CoreChain.IncrementalStateRootCalculator(node.Bundle.State).ComputeFullStateRootAsync();
                    Assert.Equal(full.ToHex(), carried.StateRoot.ToHex());
                }

                Assert.DoesNotContain(log.Messages, m => m.Contains("DevP2P follower exited"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("EVM bug"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("REVERTED"));
            }
            finally
            {
                await second.DisposeAsync();
                await first.DisposeAsync();
            }
        }

        private static Nethereum.Model.ISignedTransaction SignedTransfer(BigInteger chainId, EthECKey sender, int nonce)
        {
            var tx = new Nethereum.Model.Transaction1559(
                chainId: chainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero, maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(1000000), receiverAddress: "0x000000000000000000000000000000000000dEaD",
                amount: BigInteger.One, data: null, accessList: null);
            var signature = sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Nethereum.Model.Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx;
        }

        private static void AnnounceBlockOne(AppChainComposedNode node) => node.ChainNode.BlockRangeAnnouncer!.Signal();

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, EthECKey cliqueSignerKey,
            string[] initialSigners, EthECKey nodeKey, int listenPort, string unreachablePeer)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "CliqueSiblingConvergenceE2E" };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = cliqueSignerKey.GetPrivateKey();
            config.Consensus.Clique.InitialSigners = initialSigners;
            config.Consensus.Clique.PeriodSeconds = 1;
            config.Consensus.Clique.EpochLength = 30000;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = listenPort;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = new[] { unreachablePeer };
            config.Node.Network.TargetPeerCount = 1;
            config.Node.Network.DialBudgetPerSecond = 0;
            config.Node.Network.MaxPeersPerIPv4Subnet = 0;
            config.Node.Network.MaxPeersPerIPv6Subnet = 0;
            config.Node.Network.MaxInboundPeers = 4;
            config.Node.Network.MaxInboundPerIP = 4;
            config.Node.Sync.Mode = SyncMode.ForwardExecute;
            config.Mud.DeployWorld = false;
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

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false)) return true;
                await Task.Delay(100).ConfigureAwait(false);
            }
            return await condition().ConfigureAwait(false);
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
