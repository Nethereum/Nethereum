using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain;
using Nethereum.AppChain.P2P.BlockHandling;
using Nethereum.AppChain.P2P.DotNetty;
using Nethereum.AppChain.Sequencer;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.P2P;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AppChain.P2P.IntegrationTests.Fixtures
{
    public class CliqueClusterFixture : IAsyncLifetime
    {
        public const int CHAIN_ID = 420420;
        public const int BASE_PORT = 30400;

        private readonly List<CliqueNodeInstance> _nodes = new();
        private readonly ILoggerFactory _loggerFactory;
        private bool _sequencersStarted;

        public IReadOnlyList<CliqueNodeInstance> Nodes => _nodes;

        public static readonly string[] SignerPrivateKeys = new[]
        {
            "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80",
            "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d",
            "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
        };

        public string[] SignerAddresses { get; private set; } = Array.Empty<string>();

        public CliqueClusterFixture()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss.fff] ";
                    options.SingleLine = true;
                    options.IncludeScopes = false;
                });
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddFilter("Nethereum.AppChain.Sequencer", LogLevel.Debug);
                builder.AddFilter("Nethereum.Consensus.Clique", LogLevel.Debug);
                builder.AddFilter("Nethereum.AppChain.P2P.IntegrationTests", LogLevel.Information);
            });
        }

        public async Task InitializeAsync()
        {
            SignerAddresses = new string[SignerPrivateKeys.Length];
            for (int i = 0; i < SignerPrivateKeys.Length; i++)
            {
                var key = new EthECKey(SignerPrivateKeys[i]);
                SignerAddresses[i] = key.GetPublicAddress().ToLowerInvariant();
            }

            var node0 = await CreateNodeAsync(0, null, null);
            _nodes.Add(node0);

            var genesisHeader = await node0.AppChain.GetLatestBlockAsync();
            var genesisHash = await node0.AppChain.Blocks.GetHashByNumberAsync(0);

            for (int i = 1; i < SignerPrivateKeys.Length; i++)
            {
                var node = await CreateNodeAsync(i, genesisHeader, genesisHash);
                _nodes.Add(node);
            }

            foreach (var node in _nodes)
            {
                await node.Transport.StartAsync();
            }

            await Task.Delay(500);

            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var endpoint = $"127.0.0.1:{BASE_PORT + j}";
                    await _nodes[i].Transport.ConnectAsync($"node-{j}", endpoint);
                }
            }

            await Task.Delay(1000);
        }

        private async Task<CliqueNodeInstance> CreateNodeAsync(int index, BlockHeader? sharedGenesis, byte[]? sharedGenesisHash)
        {
            var privateKey = SignerPrivateKeys[index];
            var account = new Nethereum.Web3.Accounts.Account(privateKey, CHAIN_ID);
            var port = BASE_PORT + index;

            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var appChainConfig = AppChainConfig.CreateWithName($"CliqueTestNode{index}", CHAIN_ID);
            appChainConfig.SequencerAddress = account.Address;
            appChainConfig.Coinbase = account.Address;
            appChainConfig.BaseFee = 0;
            appChainConfig.BlockGasLimit = 30_000_000;

            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore);

            if (sharedGenesis != null && sharedGenesisHash != null)
            {
                await blockStore.SaveAsync(sharedGenesis, sharedGenesisHash);
                foreach (var addr in SignerAddresses)
                {
                    var acc = new Nethereum.Model.Account
                    {
                        Balance = Web3.Web3.Convert.ToWei(10000),
                        Nonce = BigInteger.Zero
                    };
                    await stateStore.SaveAccountAsync(addr, acc);
                }
                await appChain.InitializeAsync(new GenesisOptions());
            }
            else
            {
                var genesisOptions = new GenesisOptions
                {
                    PrefundedAddresses = SignerAddresses,
                    PrefundBalance = Web3.Web3.Convert.ToWei(10000),
                    DeployCreate2Factory = false
                };
                await appChain.InitializeAsync(genesisOptions);
            }

            var transportConfig = new DotNettyConfig
            {
                ListenAddress = "127.0.0.1",
                ListenPort = port,
                MaxConnections = 10,
                ConnectionTimeoutMs = 5000,
                NodePrivateKey = privateKey,
                ChainId = CHAIN_ID
            };

            var transport = new DotNettyTransport(
                transportConfig,
                _loggerFactory.CreateLogger<DotNettyTransport>());

            var cliqueConfig = new CliqueConfig
            {
                BlockPeriodSeconds = 1,
                EpochLength = 30000,
                WiggleTimeMs = 200,
                InitialSigners = new List<string>(SignerAddresses),
                LocalSignerAddress = account.Address,
                LocalSignerPrivateKey = privateKey,
                AllowEmptyBlocks = false,
                EnableVoting = true
            };

            var cliqueEngine = new CliqueEngine(
                cliqueConfig,
                _loggerFactory.CreateLogger<CliqueEngine>());
            cliqueEngine.ApplyGenesisSigners(new List<string>(SignerAddresses));

            var cliqueStrategy = new CliqueBlockProductionStrategy(
                appChainConfig,
                cliqueEngine,
                _loggerFactory.CreateLogger<CliqueBlockProductionStrategy>());

            var sequencerConfig = new SequencerConfig
            {
                BlockTimeMs = 1000,
                MaxTransactionsPerBlock = 1000,
                AllowEmptyBlocks = false,
                BlockProductionMode = BlockProductionMode.Interval
            };

            var sequencer = new Sequencer.Sequencer(
                appChain,
                sequencerConfig,
                blockProductionStrategy: cliqueStrategy,
                logger: _loggerFactory.CreateLogger<Sequencer.Sequencer>(),
                nodeId: $"Node-{index}");

            var p2pBlockHandler = new P2PBlockHandler(
                blockStore,
                cliqueEngine,
                _loggerFactory.CreateLogger<P2PBlockHandler>());

            var p2pBroadcaster = new P2PBlockBroadcaster(
                transport,
                _loggerFactory.CreateLogger<P2PBlockBroadcaster>());

            var p2pDispatcher = new P2PMessageDispatcher(
                transport,
                p2pBlockHandler,
                _loggerFactory.CreateLogger<P2PMessageDispatcher>());

            var nodeLogger = _loggerFactory.CreateLogger<CliqueClusterFixture>();
            cliqueStrategy.BlockFinalized += async (sender, e) =>
            {
                try
                {
                    nodeLogger.LogInformation("[Node {Index}] Block {BlockNumber} finalized, ParentHash: {ParentHash}",
                        index,
                        e.Header.BlockNumber,
                        e.Header.ParentHash != null
                            ? BitConverter.ToString(e.Header.ParentHash).Replace("-", "").ToLowerInvariant().Substring(0, 16) + "..."
                            : "null");

                    var txHashes = new List<byte[]>();
                    foreach (var txResult in e.Result.TransactionResults)
                    {
                        if (txResult.TxHash != null)
                        {
                            txHashes.Add(txResult.TxHash);
                        }
                    }
                    await p2pBroadcaster.BroadcastBlockAsync(e.Header, txHashes.ToArray());
                }
                catch (Exception ex)
                {
                    nodeLogger.LogError(ex, "[Node {Index}] Error broadcasting block", index);
                }
            };

            return new CliqueNodeInstance
            {
                Index = index,
                Account = account,
                AppChain = appChain,
                Transport = transport,
                CliqueEngine = cliqueEngine,
                CliqueStrategy = cliqueStrategy,
                Sequencer = sequencer,
                Port = port,
                BlockHandler = p2pBlockHandler,
                Broadcaster = p2pBroadcaster,
                Dispatcher = p2pDispatcher
            };
        }

        public async Task StartAllNodesAsync()
        {
            if (_sequencersStarted)
                return;

            foreach (var node in _nodes)
            {
                await node.Sequencer.StartAsync();
            }

            _sequencersStarted = true;
            await Task.Delay(2000);
        }

        public async Task StopAllNodesAsync()
        {
            foreach (var node in _nodes)
            {
                await node.Sequencer.StopAsync();
                await node.Transport.StopAsync();
            }
        }

        public async Task DisposeAsync()
        {
            await StopAllNodesAsync();

            foreach (var node in _nodes)
            {
                node.Dispatcher?.Dispose();
                await node.Transport.DisposeAsync();
                node.CliqueEngine.Dispose();
            }

            _loggerFactory.Dispose();
        }
    }

    public class CliqueNodeInstance
    {
        public int Index { get; set; }
        public Nethereum.Web3.Accounts.Account Account { get; set; } = null!;
        public IAppChain AppChain { get; set; } = null!;
        public DotNettyTransport Transport { get; set; } = null!;
        public CliqueEngine CliqueEngine { get; set; } = null!;
        public CliqueBlockProductionStrategy CliqueStrategy { get; set; } = null!;
        public Sequencer.Sequencer Sequencer { get; set; } = null!;
        public int Port { get; set; }
        public P2PBlockHandler BlockHandler { get; set; } = null!;
        public P2PBlockBroadcaster Broadcaster { get; set; } = null!;
        public P2PMessageDispatcher Dispatcher { get; set; } = null!;
    }
}
