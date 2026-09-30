using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class SnapFollowerNode : IDisposable
    {
        private readonly IChainStoreBundle _bundle;
        private readonly string _dataDir;
        private readonly BigInteger _chainId;
        private readonly string _sequencerAddress;
        private readonly byte[] _pivotStateRoot;

        public IStateStore State => _bundle.State;
        public IBlockStore Blocks => _bundle.Blocks;
        public IStateDiffStore Diffs => _bundle.Diffs;
        public byte[] PivotStateRoot => _pivotStateRoot;

        public TrieFallbackStateStore RecoveredState()
            => new TrieFallbackStateStore(_bundle.State, (INodeBlobStore)_bundle.TrieNodes, () => _pivotStateRoot);

        private SnapFollowerNode(IChainStoreBundle bundle, string dataDir, BigInteger chainId, string sequencerAddress, byte[] pivotStateRoot)
        {
            _bundle = bundle;
            _dataDir = dataDir;
            _chainId = chainId;
            _sequencerAddress = sequencerAddress;
            _pivotStateRoot = pivotStateRoot;
        }

        public static async Task<SnapFollowerNode> SnapAsync(InProcessSequencerDriver sequencer, BlockHeader pivotHeader, byte[] pivotHash)
        {
            var bytecodes = new Dictionary<string, byte[]>();
            foreach (var kv in await sequencer.State.GetAllAccountsAsync())
            {
                var codeHash = kv.Value.CodeHash;
                if (codeHash == null || codeHash.Length == 0) continue;
                var code = await sequencer.State.GetCodeAsync(codeHash);
                if (code != null && code.Length > 0) bytecodes[codeHash.ToHex()] = code;
            }
            var handler = new PatriciaSnapRequestHandler(sequencer.TrieNodes, new DictBytecodeStore(bytecodes));
            var peer = new InProcessSnapPeer(handler);

            var dataDir = Path.Combine(Path.GetTempPath(), "snap-fwd-" + Guid.NewGuid().ToString("N"));
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dataDir });
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dataDir, journalOptions: HistoricalStateOptions.FullArchive);
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

            var result = await SnapBootstrapper.RunAsync(bundle, peer, pivotHeader, pivotHash, NullLogger.Instance);
            if (!result.Ran) throw new Exception($"snap did not run: {result.SkipReason}");

            await result.StateCompaction.ConfigureAwait(false);

            return new SnapFollowerNode(bundle, dataDir, sequencer.ChainId, sequencer.SequencerAddress, pivotHeader.StateRoot);
        }

        public async Task ForwardExecuteAsync(IEnumerable<(BlockHeader Header, IList<ISignedTransaction> Transactions)> blocks)
        {
            var chainConfig = new ChainConfig { ChainId = _chainId, BaseFee = BigInteger.Zero, Coinbase = _sequencerAddress };
            var activations = new FixedChainActivations(HardforkNames.Parse(chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();

            var calc = new IncrementalStateRootCalculator(_bundle.State, _bundle.TrieNodes);
            var engine = new BlockExecutor(
                _bundle.State, _bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: _bundle.TrieNodes);
            var importer = new BlockImporter(engine, _bundle.Blocks, _bundle.State, _bundle.Transactions,
                _bundle.Receipts, _bundle.Logs, uncleStore: _bundle.Uncles);

            foreach (var (header, txs) in blocks)
            {
                var result = await importer.ImportAsync(header, txs, null, null);
                if (!result.RootMatches)
                    throw new Exception($"Snap follower diverged forward-executing block {header.BlockNumber}");
                _bundle.Metadata.Commit((ulong)header.BlockNumber, result.BlockHash);
            }
        }

        public async Task<bool> TryForwardExecuteFlatStateOnlyAsync(IEnumerable<(BlockHeader Header, IList<ISignedTransaction> Transactions)> blocks)
        {
            var chainConfig = new ChainConfig { ChainId = _chainId, BaseFee = BigInteger.Zero, Coinbase = _sequencerAddress };
            var activations = new FixedChainActivations(HardforkNames.Parse(chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();

            var calc = new IncrementalStateRootCalculator(_bundle.State, _bundle.TrieNodes);
            var engine = new BlockExecutor(
                _bundle.State, _bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: _bundle.TrieNodes);
            var importer = new BlockImporter(engine, _bundle.Blocks, _bundle.State, _bundle.Transactions,
                _bundle.Receipts, _bundle.Logs, uncleStore: _bundle.Uncles);

            foreach (var (header, txs) in blocks)
            {
                try
                {
                    var result = await importer.ImportAsync(header, txs, null, null);
                    if (!result.RootMatches) return false;
                    _bundle.Metadata.Commit((ulong)header.BlockNumber, result.BlockHash);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }

        public void Dispose()
        {
            _bundle.Dispose();
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, true); } catch { }
        }

        private sealed class DictBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<string, byte[]> _codes;
            public DictBytecodeStore(Dictionary<string, byte[]> codes) => _codes = codes;
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }
    }
}
