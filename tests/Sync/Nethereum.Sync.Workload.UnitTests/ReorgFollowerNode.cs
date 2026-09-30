using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;
using AppChainCore = Nethereum.AppChain.AppChain;

using Nethereum.Chain.TestData;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class ReorgFollowerNode : IDisposable
    {
        private readonly IChainStoreBundle _bundle;
        private readonly string _dataDir;
        private readonly ChainConfig _chainConfig;
        private BlockImporter _importer;

        public IBlockStore Blocks => _bundle.Blocks;
        public IStateStore State => _bundle.State;
        public ChainStores Stores => new ChainStores(
            _bundle.Blocks, _bundle.State, _bundle.Transactions, _bundle.Receipts, _bundle.Logs, _bundle.BlockAccessLists);

        private ReorgFollowerNode(IChainStoreBundle bundle, string dataDir, ChainConfig chainConfig)
        {
            _bundle = bundle;
            _dataDir = dataDir;
            _chainConfig = chainConfig;
            _importer = BuildImporter();
        }

        private BlockImporter BuildImporter()
        {
            var activations = new FixedChainActivations(HardforkNames.Parse(_chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = _chainConfig.GetHardforkConfig();
            var calc = new IncrementalStateRootCalculator(_bundle.State, _bundle.TrieNodes);
            var engine = new BlockExecutor(
                _bundle.State, _bundle.Blocks, activations,
                chainConfigFactory: _ => _chainConfig,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: _bundle.TrieNodes);
            return new BlockImporter(engine, _bundle.Blocks, _bundle.State, _bundle.Transactions,
                _bundle.Receipts, _bundle.Logs, uncleStore: _bundle.Uncles,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: _bundle.BlockAccessLists);
        }

        public static async Task<ReorgFollowerNode> CreateAsync(InProcessSequencerDriver sequencer)
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "sync-reorg-" + Guid.NewGuid().ToString("N"));
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dataDir });
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dataDir, journalOptions: HistoricalStateOptions.FullArchive);

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(InProcessSequencerDriver.ChainName, sequencer.ChainId);
            config.SequencerAddress = sequencer.SequencerAddress;
            config.Hardfork = sequencer.Hardfork;
            var appChain = new AppChainCore(config, bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, bundle.TrieNodes);
            await appChain.InitializeAsync(sequencer.Genesis);

            var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0);
            bundle.Metadata.Commit(0, genesisHash);

            var chainConfig = new ChainConfig
            {
                ChainId = sequencer.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = sequencer.SequencerAddress,
                Hardfork = sequencer.Hardfork
            };
            return new ReorgFollowerNode(bundle, dataDir, chainConfig);
        }

        public async Task ImportAsync(IEnumerable<(BlockHeader Header, IList<ISignedTransaction> Transactions)> blocks)
        {
            foreach (var (header, txs) in blocks)
            {
                var result = await _importer.ImportAsync(header, txs, null, null);
                if (!result.RootMatches)
                    throw new Exception($"Follower diverged at block {header.BlockNumber}");
                _bundle.Metadata.Commit((ulong)header.BlockNumber, result.BlockHash);
            }
        }

        public async Task RewindToAsync(ulong target)
        {
            var coordinator = new RewindCoordinator(_bundle);
            var result = await coordinator.RewindToAsync(target, RewindPolicy.JournalOnly);
            if (result.Outcome != RewindOutcome.JournalUsed)
                throw new Exception($"rewind to {target} failed: {result.Outcome} ({result.Detail})");

            _importer = BuildImporter();
        }

        public void Dispose()
        {
            _bundle.Dispose();
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, true); } catch { }
        }
    }
}
