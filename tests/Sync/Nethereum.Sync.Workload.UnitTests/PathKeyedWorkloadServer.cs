using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class PathKeyedWorkloadServer : IDisposable
    {
        private static readonly BigInteger DefaultChainId = new BigInteger(420420);

        public RocksDbChainStoreBundle Bundle { get; private set; }
        public FollowerChainNode Node { get; private set; }
        public byte[][] Roots { get; private set; }
        public ulong Head { get; private set; }
        public ulong Floor { get; private set; }
        public bool PathKeyed { get; private set; }
        public BigInteger ChainId { get; private set; }
        public string SequencerAddress { get; private set; }

        public PatriciaSnapRequestHandler SnapHandler { get; private set; }

        private string _dir;
        private PeerListener _listener;
        private EthECKey _wireKey;

        public byte[] GenesisHash { get; private set; }
        public ulong NetworkId { get; private set; }
        public string Enode { get; private set; }

        private PathKeyedWorkloadServer() { }

        public static async Task<PathKeyedWorkloadServer> CreateAsync(
            InProcessSequencerDriver sequencer,
            bool pathKeyed = true,
            int trieNodeHistoryBlocks = 32,
            bool historyIndex = true)
        {
            var self = new PathKeyedWorkloadServer
            {
                PathKeyed = pathKeyed,
                ChainId = sequencer.ChainId,
                SequencerAddress = sequencer.SequencerAddress,
            };
            self._dir = Path.Combine(Path.GetTempPath(), "wl-pathkeyed-" + Guid.NewGuid().ToString("N"));

            RocksDbStorageOptions storageOptions = pathKeyed
                ? new RocksDbStorageOptions
                {
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = trieNodeHistoryBlocks,
                    TrieNodeHistoryIndex = historyIndex,
                }
                : null;

            var bundle = RocksDbChainStoreBundle.Open(
                self._dir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: storageOptions);
            self.Bundle = bundle;

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(InProcessSequencerDriver.ChainName, sequencer.ChainId);
            config.SequencerAddress = sequencer.SequencerAddress;
            var appChain = new AppChainCore(config, bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, bundle.StateTrieNodes);
            await appChain.InitializeAsync(sequencer.Genesis);

            var chainConfig = new ChainConfig { ChainId = sequencer.ChainId, BaseFee = BigInteger.Zero, Coinbase = sequencer.SequencerAddress };
            var activations = new FixedChainActivations(HardforkNames.Parse(chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new IncrementalStateRootCalculator(
                bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            var engine = new BlockExecutor(
                bundle.State, bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc, rewardPolicy: NoRewardPolicy.Instance, trieNodeStore: bundle.TrieNodes);
            var importer = new BlockImporter(
                engine, bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                uncleStore: bundle.Uncles, nodeCommitBlockContext: bundle.NodeCommitBlockSource,
                atomicFlush: bundle as IAtomicBlockFlush);

            var genesisHeader = await bundle.Blocks.GetByNumberAsync(0);
            var roots = new List<byte[]> { genesisHeader.StateRoot };
            foreach (var (header, txs) in sequencer.ProducedBlockData)
            {
                var blockWithdrawals = await sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber);
                var r = await importer.ImportAsync(header, txs, null, blockWithdrawals);
                if (!r.RootMatches)
                    throw new Exception($"path-keyed server diverged re-executing block {header.BlockNumber}");
                if (blockWithdrawals != null && blockWithdrawals.Count > 0)
                    await bundle.Withdrawals.SaveAsync(r.BlockHash, blockWithdrawals);
                bundle.Metadata.Commit((ulong)header.BlockNumber, r.BlockHash);
                roots.Add(header.StateRoot);
            }

            self.Roots = roots.ToArray();
            self.Head = (ulong)await bundle.Blocks.GetHeightAsync();
            self.Floor = bundle.NodeServing?.Floor.FloorFor(self.Head)
                         ?? (pathKeyed && trieNodeHistoryBlocks > 0
                             ? (self.Head > (ulong)trieNodeHistoryBlocks ? self.Head - (ulong)trieNodeHistoryBlocks : 0UL)
                             : 0UL);

            self.Node = BuildNode(bundle, sequencer.ChainId, chainConfig, hardforkConfig);
            self.SnapHandler = BuildSnapHandler(bundle);
            return self;
        }

        private static FollowerChainNode BuildNode(
            RocksDbChainStoreBundle bundle, BigInteger chainId, ChainConfig chainConfig, HardforkConfig hardforkConfig)
        {
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);
            return new FollowerChainNode(
                bundle: bundle,
                source: new InertBlockSource(),
                executorFactory: _ => throw new InvalidOperationException("executor not used in this read-only proof driver"),
                policy: new FixedPolicy(),
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier);
        }

        private static PatriciaSnapRequestHandler BuildSnapHandler(RocksDbChainStoreBundle bundle)
        {
            var bytecodes = new StateStoreBytecodeStore(bundle.State);
            if (bundle.LatestProofNodeStore != null)
                return new PatriciaSnapRequestHandler(
                    bundle.LatestProofNodeStore, bytecodes, selector: bundle.NodeServing as ISnapNodeStoreSelector);
            return new PatriciaSnapRequestHandler(bundle.TrieNodes, bytecodes);
        }

        public async Task<RpcResponseMessage> GetProofAsync(string address, string[] storageKeys, string blockParam)
        {
            var handler = new EthGetProofHandler();
            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(Node, ChainId, services);
            var request = new RpcRequestMessage(1, "eth_getProof", address, storageKeys ?? new string[0], blockParam);
            return await handler.HandleAsync(request, context);
        }

        public async Task<string> StartWire()
        {
            GenesisHash = await Bundle.Blocks.GetHashByNumberAsync(0);
            var bestHash = await Bundle.Blocks.GetHashByNumberAsync(Head);
            NetworkId = (ulong)ChainId;

            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = bestHash,
                GenesisHash = GenesisHash,
                ForkHash = 0xAABBCCDDu,
                ForkNext = 0
            };
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = true,
                MirrorRemoteStatus = false,
                ClientId = "Nethereum.Sync.PathKeyedWireServer/1.0",
            };
            _wireKey = EthECKey.GenerateKey();
            _listener = new PeerListener(_wireKey, Bundle, options, status, SnapHandler);
            await _listener.StartAsync();
            Enode = $"enode://{_wireKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{_listener.Port}";
            return Enode;
        }

        public void Dispose()
        {
            try { _listener?.Dispose(); } catch { }
            try { Node?.Dispose(); } catch { }
            try { if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private sealed class InertBlockSource : IBlockSource
        {
            public DivergenceSignal LastChainBreak => null;
            public async IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
            { await Task.CompletedTask; yield break; }
            public Task<BlockSourceHealth> GetHealthAsync(System.Threading.CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);
            public Task ReportBadBundleAsync(ulong b, BadBundleReason r, System.Threading.CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => ValidationAction.RewindAndRetry;
        }
    }
}
