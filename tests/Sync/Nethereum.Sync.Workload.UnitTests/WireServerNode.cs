using Nethereum.DevP2P.Sync.Peering;
using Nethereum.CoreChain.Validation;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using AppChainCore = Nethereum.AppChain.AppChain;

using Nethereum.Chain.TestData;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class WireServerNode : IAsyncDisposable
    {
        private readonly PeerListener _listener;
        private readonly TaskCompletionSource<string> _inboundAdmitted;

        public Task<string> InboundAdmitted => _inboundAdmitted.Task;

        public IChainStoreBundle Bundle { get; }
        public byte[] GenesisHash { get; }
        public ulong NetworkId { get; }
        public string Enode { get; }
        public int Port => _listener.Port;
        public ChainStores Stores => new ChainStores(
            Bundle.Blocks, Bundle.State, Bundle.Transactions, Bundle.Receipts, Bundle.Logs, Bundle.BlockAccessLists);

        private readonly Nethereum.ChainNode.Hosting.ChainNode _composed;
        private readonly InProcessSequencerDriver _sequencer;
        private int _importedBlockCount;

        private WireServerNode(
            Nethereum.ChainNode.Hosting.ChainNode composed, PeerListener listener, IChainStoreBundle bundle,
            byte[] genesisHash, ulong networkId, string enode, TaskCompletionSource<string> inboundAdmitted,
            InProcessSequencerDriver sequencer)
        {
            _composed = composed;
            _listener = listener;
            Bundle = bundle;
            GenesisHash = genesisHash;
            NetworkId = networkId;
            Enode = enode;
            _inboundAdmitted = inboundAdmitted;
            _sequencer = sequencer;
            _importedBlockCount = sequencer.ProducedBlockData.Count;
        }

        public async Task<int> ImportPendingProducedBlocksAsync()
        {
            var data = _sequencer.ProducedBlockData;
            var importer = BuildImporter(Bundle, _sequencer);
            var imported = 0;
            while (_importedBlockCount < data.Count)
            {
                var (header, txs) = data[_importedBlockCount];
                var blockWithdrawals = await _sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber).ConfigureAwait(false);
                var r = await importer.ImportAsync(
                    header, txs, null,
                    blockWithdrawals).ConfigureAwait(false);

                if (!r.RootMatches)
                    throw new Exception($"server diverged live-importing block {header.BlockNumber}");

                if (blockWithdrawals != null && blockWithdrawals.Count > 0)
                    await Bundle.Withdrawals.SaveAsync(r.BlockHash, blockWithdrawals).ConfigureAwait(false);

                Bundle.Metadata.Commit((ulong)header.BlockNumber, r.BlockHash);
                _importedBlockCount++;
                imported++;
            }
            return imported;
        }

        private static BlockImporter BuildImporter(IChainStoreBundle bundle, InProcessSequencerDriver sequencer)
        {
            var chainConfig = new ChainConfig
            {
                ChainId = sequencer.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = sequencer.SequencerAddress,
                Hardfork = sequencer.Hardfork
            };
            var activations = new FixedChainActivations(HardforkNames.Parse(chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new BlockExecutor(
                bundle.State, bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc, rewardPolicy: NoRewardPolicy.Instance, trieNodeStore: bundle.TrieNodes);
            return new BlockImporter(
                engine, bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                uncleStore: bundle.Uncles,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: bundle.BlockAccessLists);
        }

        public static async Task<WireServerNode> StartAsync(
            InProcessSequencerDriver sequencer,
            int snapResponseLimit = PatriciaSnapRequestHandler.SoftResponseLimit,
            TimeSpan? idleTimeout = null,
            bool advertiseSnap2 = false)
        {
            var inboundAdmitted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var config = new Nethereum.ChainNode.Hosting.Configuration.ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.Serve = true;
            config.Network.ListenPort = 0;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.MaxInboundPeers = 5;
            config.Network.MaxInboundPerIP = 5;
            if (idleTimeout.HasValue) config.Network.IdleTimeout = idleTimeout.Value;
            config.Network.ClientId = "Nethereum.Sync.WireServer/1.0";
            config.Sync.Mode = Nethereum.ChainNode.Hosting.Configuration.SyncMode.None;
            config.Sync.Snap.SoftResponseLimit = snapResponseLimit;
            config.Sync.Snap.AdvertiseSnap2 = advertiseSnap2;

            var composed = await Nethereum.ChainNode.Hosting.ChainNode.StartAsync(
                new WorkloadChainDefinition(sequencer),
                config,
                loggerFactory: null,
                callbacks: new Nethereum.ChainNode.Hosting.ChainNodeServeCallbacks
                {
                    InboundPeerAdded = key => inboundAdmitted.TrySetResult(key),
                }).ConfigureAwait(false);

            var enode = $"enode://{composed.Config.ResolveNodeKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{composed.Listener.Port}";

            return new WireServerNode(
                composed, composed.Listener, composed.Bundle,
                composed.Profile.GenesisHash, composed.Profile.NetworkId, enode, inboundAdmitted, sequencer);
        }

        private sealed class WorkloadChainDefinition : Nethereum.ChainNode.Hosting.IChainDefinition
        {
            private readonly InProcessSequencerDriver _sequencer;
            private byte[] _genesisHash;

            public WorkloadChainDefinition(InProcessSequencerDriver sequencer) => _sequencer = sequencer;

            public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
            {
                var config = Nethereum.AppChain.AppChainConfig.CreateWithName(
                    InProcessSequencerDriver.ChainName, _sequencer.ChainId);
                config.SequencerAddress = _sequencer.SequencerAddress;
                config.Hardfork = _sequencer.Hardfork;

                var appChain = new AppChainCore(
                    config, bundle.Blocks, bundle.Transactions, bundle.Receipts,
                    bundle.Logs, bundle.State, bundle.TrieNodes);
                await appChain.InitializeAsync(_sequencer.Genesis).ConfigureAwait(false);

                await ReplayWorkloadAsync(bundle).ConfigureAwait(false);

                _genesisHash = await bundle.Blocks.GetHashByNumberAsync(0).ConfigureAwait(false);
            }

            public Task<Nethereum.ChainNode.Hosting.IChainProfile> CreateProfileAsync(IChainStoreBundle bundle) =>
                Task.FromResult<Nethereum.ChainNode.Hosting.IChainProfile>(
                    new WorkloadChainProfile((ulong)_sequencer.ChainId, _genesisHash));

            public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => null;

            private async Task ReplayWorkloadAsync(IChainStoreBundle bundle)
            {
                var importer = BuildImporter(bundle, _sequencer);

                foreach (var (header, txs) in _sequencer.ProducedBlockData)
                {
                    var blockWithdrawals = await _sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber);
                    var r = await importer.ImportAsync(
                        header, txs, null,
                        blockWithdrawals);

                    if (!r.RootMatches)
                        throw new Exception($"server diverged re-executing block {header.BlockNumber}");

                    if (blockWithdrawals != null && blockWithdrawals.Count > 0)
                        await bundle.Withdrawals.SaveAsync(r.BlockHash, blockWithdrawals);

                    bundle.Metadata.Commit((ulong)header.BlockNumber, r.BlockHash);
                }
            }
        }

        private sealed class WorkloadChainProfile : Nethereum.ChainNode.Hosting.IChainProfile
        {
            public WorkloadChainProfile(ulong networkId, byte[] genesisHash)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
                (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public Nethereum.DevP2P.Sync.Abstractions.IPeerHandshakeWorker CreateHandshakeWorker(
                Microsoft.Extensions.Logging.ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                new Nethereum.DevP2P.Sync.Peering.ChainPeerHandshakeWorker(
                    GenesisHash, NetworkId, ForkThresholds, ourHead: null,
                    logger: null, advertiseSnap2: advertiseSnap2);
        }

        public ValueTask DisposeAsync() => _listener.DisposeAsync();

        private sealed class DictBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<string, byte[]> _codes;
            public DictBytecodeStore(Dictionary<string, byte[]> codes) => _codes = codes;
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }

    }
}
