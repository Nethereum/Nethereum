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
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class CliqueClusterStateRootConsistencyTests
    {
        private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

        [Fact]
        public async Task Given_TwoSignersSealingBlocksThatCarryTransactions_When_EachImportsTheOthersBlocks_Then_EveryHeaderRootEqualsTheFullRecomputeOfFlatStateAndNoFollowerStops()
        {
            var log = new CapturingLoggerProvider();
            var loggerFactory = LoggerFactory.Create(b => { b.AddProvider(log); b.SetMinimumLevel(LogLevel.Information); });

            var chainId = new BigInteger(420423_000 + Environment.TickCount % 1000);
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
                first.ChainNode.Sync!.Pool.EnqueueCandidate(EnodeOf(secondNodeKey, ports[1]));
                second.ChainNode.Sync!.Pool.EnqueueCandidate(EnodeOf(firstNodeKey, ports[0]));
                Assert.True(await WaitUntilAsync(() => Task.FromResult(
                    first.ChainNode.Sync!.Pool.ActivePeers.Count > 0 && second.ChainNode.Sync!.Pool.ActivePeers.Count > 0), SettleTimeout),
                    "the signers never peered");

                await first.Sequencer!.ProduceBlockAsync();
                Assert.True(await SameHashAsync(first, second, 1), "second never imported block 1: " + Tail(log));

                await second.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, genesisOwnerKey, nonce: 0));
                Assert.True(await SameHashAsync(first, second, 2), "first never imported block 2: " + Tail(log));

                await first.Sequencer!.ProduceBlockAsync();
                Assert.True(await SameHashAsync(first, second, 3), "second never imported block 3: " + Tail(log));

                await second.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, genesisOwnerKey, nonce: 1));
                Assert.True(await SameHashAsync(first, second, 4), "first never imported block 4: " + Tail(log));

                foreach (var node in new[] { first, second })
                {
                    var head = await node.Bundle.Blocks.GetByNumberAsync(4);
                    var full = await new IncrementalStateRootCalculator(node.Bundle.State).ComputeFullStateRootAsync();
                    Assert.Equal(full.ToHex(), head.StateRoot.ToHex());
                }

                Assert.True(!log.Messages.Any(m => m.Contains("REVERTED")), string.Join(" | ", log.Messages.Where(m => m.Contains("REVERTED"))));
                Assert.DoesNotContain(log.Messages, m => m.Contains("DevP2P follower exited"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("EVM bug"));
            }
            finally
            {
                await second.DisposeAsync();
                await first.DisposeAsync();
            }
        }

        private static string Tail(CapturingLoggerProvider log) =>
            string.Join(" | ", log.Messages.Where(m => !m.Contains("block.exec.timing")).TakeLast(40));

        private static ISignedTransaction SignedTransfer(BigInteger chainId, EthECKey sender, int nonce)
        {
            var tx = new Transaction1559(
                chainId: chainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero, maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(1000000), receiverAddress: "0x000000000000000000000000000000000000dEaD",
                amount: BigInteger.One, data: null, accessList: null);
            var signature = sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx;
        }

        private static Task<bool> SameHashAsync(AppChainComposedNode a, AppChainComposedNode b, int number) =>
            WaitUntilAsync(async () =>
            {
                var x = await a.Bundle.Blocks.GetHashByNumberAsync(number);
                var y = await b.Bundle.Blocks.GetHashByNumberAsync(number);
                return x != null && y != null && x.ToHex() == y.ToHex();
            }, SettleTimeout);

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, EthECKey cliqueSignerKey,
            string[] initialSigners, EthECKey nodeKey, int listenPort, string unreachablePeer)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "CliqueStateRootConsistencyE2E" };
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
                    LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter)
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
