using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    public class CliqueRocksDbStateRootConsistencyTests
    {
        private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_TwoRocksDbBackedSignersWhereOneRestartsOnItsDataDirectory_When_BlocksCarryingTransactionsAreSealedOnBothSides_Then_EveryHashAgreesAndTheHeadRootEqualsTheFullRecomputeOnBothNodes(bool pathKeyedState)
        {
            var root = Path.Combine(Path.GetTempPath(), "clique-rocksdb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var log = new CapturingLoggerProvider();
            var loggerFactory = LoggerFactory.Create(b => { b.AddProvider(log); b.SetMinimumLevel(LogLevel.Information); });

            var chainId = new BigInteger(420424_000 + Environment.TickCount % 1000);
            var genesisOwnerKey = EthECKey.GenerateKey();
            var sharedSequencerKey = EthECKey.GenerateKey();
            var firstKey = EthECKey.GenerateKey();
            var secondKey = EthECKey.GenerateKey();
            var initialSigners = new[] { firstKey.GetPublicAddress(), secondKey.GetPublicAddress() };
            var firstNodeKey = EthECKey.GenerateKey();
            var secondNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);
            var firstEnode = EnodeOf(firstNodeKey, ports[0]);
            var secondEnode = EnodeOf(secondNodeKey, ports[1]);
            var firstDir = Path.Combine(root, "first");
            var secondDir = Path.Combine(root, "second");

            AppChainServerConfig FirstConfig() => BuildConfig(
                chainId, genesisOwnerKey, sharedSequencerKey, firstKey, initialSigners, firstNodeKey, ports[0], secondEnode,
                firstDir, pathKeyedState);

            AppChainComposedNode? first = null;
            AppChainComposedNode? second = null;
            try
            {
                first = await AppChainComposition.ComposeAsync(FirstConfig(), loggerFactory, CancellationToken.None);
                second = await AppChainComposition.ComposeAsync(
                    BuildConfig(chainId, genesisOwnerKey, sharedSequencerKey, secondKey, initialSigners, secondNodeKey, ports[1], firstEnode,
                        secondDir, pathKeyedState),
                    loggerFactory, CancellationToken.None);

                await PeerAsync(log, first, second, firstNodeKey, secondNodeKey, ports);

                await first.Sequencer!.ProduceBlockAsync();
                Assert.True(await SameHashAsync(first, second, 1), "second never imported block 1: " + Tail(log));

                await second.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, genesisOwnerKey, nonce: 0));
                Assert.True(await SameHashAsync(first, second, 2), "first never imported block 2: " + Tail(log));

                await first.DisposeAsync();
                first = await AppChainComposition.ComposeAsync(FirstConfig(), loggerFactory, CancellationToken.None);
                await PeerAsync(log, first, second, firstNodeKey, secondNodeKey, ports);

                await first.Sequencer!.ProduceBlockAsync();
                Assert.True(await SameHashAsync(first, second, 3), "second never imported block 3 after the restart: " + string.Join(" | ", log.Messages.Where(m => m.Contains("ushed") || m.Contains("ejected") || m.Contains("ivergence") || m.Contains("bug") || m.Contains("atal") || m.Contains("ompet") || m.Contains("ismatch") || m.Contains("exited") || m.Contains("refused") || m.Contains("fork_choice") || m.Contains("peer ")).TakeLast(40)));

                await second.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, genesisOwnerKey, nonce: 1));
                Assert.True(await SameHashAsync(first, second, 4), "first never imported block 4: " + Tail(log));

                foreach (var node in new[] { first, second })
                {
                    var head = await node.Bundle.Blocks.GetByNumberAsync(4);
                    var full = await new IncrementalStateRootCalculator(node.Bundle.State).ComputeFullStateRootAsync();
                    Assert.Equal(full.ToHex(), head.StateRoot.ToHex());
                }

                Assert.DoesNotContain(log.Messages, m => m.Contains("REVERTED"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("DevP2P follower exited"));
                Assert.DoesNotContain(log.Messages, m => m.Contains("EVM bug"));
            }
            finally
            {
                if (second != null) await second.DisposeAsync();
                if (first != null) await first.DisposeAsync();
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static async Task PeerAsync(CapturingLoggerProvider log,
            AppChainComposedNode first, AppChainComposedNode second, EthECKey firstNodeKey, EthECKey secondNodeKey,
            IReadOnlyList<int> ports)
        {
            Assert.True(await WaitUntilAsync(() => Task.FromResult(
                first.ChainNode.Sync!.Pool.ActivePeers.Count > 0 && second.ChainNode.Sync!.Pool.ActivePeers.Count > 0), SettleTimeout),
                "the signers never peered " + string.Join(" | ", log.Messages.Where(m => !m.Contains("block.exec.timing")).TakeLast(40)));
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
            string[] initialSigners, EthECKey nodeKey, int listenPort, string trustedPeer,
            string dataDirectory, bool pathKeyedState)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "CliqueRocksDbStateRootConsistencyE2E" };
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
            config.Node.Storage.InMemory = false;
            config.Node.Storage.DataDirectory = dataDirectory;
            config.Node.Storage.PathKeyedState = pathKeyedState;
            config.Node.Storage.BlockCacheSize = 32L * 1024 * 1024;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = listenPort;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = new[] { trustedPeer };
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
