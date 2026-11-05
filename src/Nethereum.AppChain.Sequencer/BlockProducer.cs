using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;

namespace Nethereum.AppChain.Sequencer
{
    public class BlockProducer : IBlockProducer
    {
        private readonly IAppChain _appChain;
        private readonly CoreChain.BlockProducer _coreBlockProducer;
        private readonly AppChainConfig _config;
        private readonly IBlockProductionStrategy? _strategy;
        private readonly AppChainBlockProductionStrategy _defaultStrategy;

        public BlockProducer(
            IAppChain appChain,
            TransactionProcessor transactionProcessor,
            IBlockProductionStrategy? strategy = null,
            CoreChain.IIncrementalStateRootCalculator? stateRootCalculator = null,
            IBlockHashProvider? blockHashProvider = null,
            IBlockEncodingProvider? blockEncodingProvider = null,
            IBlockRootsProvider? blockRootsProvider = null,
            IWithdrawalStore? withdrawalStore = null,
            IBlockAccessListStore? blockAccessListStore = null)
        {
            if (appChain == null) throw new ArgumentNullException(nameof(appChain));
            if (transactionProcessor == null) throw new ArgumentNullException(nameof(transactionProcessor));

            _appChain = appChain;
            _config = appChain.Config;
            _strategy = strategy;
            _defaultStrategy = new AppChainBlockProductionStrategy(_config);
            var trieNodeStore = appChain.TrieNodes;
            var core = ExecutionStackFactory.BuildExecutor(
                appChain.State, appChain.Blocks, trieNodeStore, trieNodeStore, uncles: null,
                _config.ResolveActivations(),
                chainConfigFactory: _ => _config,
                hardforkConfigFactory: _config.ConfigForFork,
                options: new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    AuthorResolver = strategy != null ? strategy.ResolveFeeRecipient : null,
                    StateRootCalculatorFactory = stateRootCalculator != null
                        ? (Func<IStateStore, ITrieNodeStore, CoreChain.IIncrementalStateRootCalculator>)((_, _) => stateRootCalculator)
                        : null
                });
            var engine = core.Executor;
            var resolvedCalculator = core.Calculator;

            _coreBlockProducer = new CoreChain.BlockProducer(
                engine,
                appChain.Blocks,
                appChain.Transactions,
                appChain.Receipts,
                appChain.Logs,
                appChain.State,
                trieNodeStore,
                resolvedCalculator,
                orderingPolicy: null,
                blockHashProvider: blockHashProvider,
                blockEncodingProvider: blockEncodingProvider,
                blockRootsProvider: blockRootsProvider,
                withdrawalStore: withdrawalStore,
                nodeCommitBlockContext: null,
                blockAccessListStore: blockAccessListStore,
                hardforkConfigFactory: _config.ConfigForFork);
        }

        public BlockProducer(
            IAppChain appChain,
            BlockExecutor engine,
            CoreChain.IIncrementalStateRootCalculator stateRootCalculator,
            ITrieNodeStore trieNodeStore,
            IBlockProductionStrategy? strategy = null,
            IBlockHashProvider? blockHashProvider = null,
            IBlockEncodingProvider? blockEncodingProvider = null,
            IBlockRootsProvider? blockRootsProvider = null,
            IWithdrawalStore? withdrawalStore = null,
            IBlockAccessListStore? blockAccessListStore = null,
            BlockCommit? commit = null)
        {
            if (appChain == null) throw new ArgumentNullException(nameof(appChain));
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (stateRootCalculator == null) throw new ArgumentNullException(nameof(stateRootCalculator));
            if (trieNodeStore == null) throw new ArgumentNullException(nameof(trieNodeStore));

            _appChain = appChain;
            _config = appChain.Config;
            _strategy = strategy;
            _defaultStrategy = new AppChainBlockProductionStrategy(_config);

            _coreBlockProducer = new CoreChain.BlockProducer(
                engine,
                appChain.Blocks,
                appChain.Transactions,
                appChain.Receipts,
                appChain.Logs,
                appChain.State,
                trieNodeStore,
                stateRootCalculator,
                orderingPolicy: null,
                blockHashProvider: blockHashProvider,
                blockEncodingProvider: blockEncodingProvider,
                blockRootsProvider: blockRootsProvider,
                withdrawalStore: withdrawalStore,
                nodeCommitBlockContext: null,
                blockAccessListStore: blockAccessListStore,
                hardforkConfigFactory: _config.ConfigForFork,
                commit: commit);
        }

        /// <summary>
        /// Whether headers on this chain carry the EIP-7928 block access list hash and
        /// the EIP-7843 slot number. One predicate for both, because the pair is emitted
        /// together or not at all — asked through the header codec so the answer stays
        /// where the encoding rule already lives rather than restating a fork ordering.
        /// </summary>
        private bool CarriesAmsterdamHeaderFieldsAt(long blockNumber, ulong timestamp) =>
            Nethereum.Model.Codecs.BlockHeaderCodecs
                .ForFork(_config.ResolveActivations().ResolveAt(blockNumber, timestamp))
                .CarriesBlockAccessList;

        public async Task<BlockProductionResult> ProduceBlockAsync(IReadOnlyList<ISignedTransaction> transactions)
        {
            var parentHeader = await _appChain.GetLatestBlockAsync();
            var nextBlockNumber = parentHeader != null ? (long)parentHeader.BlockNumber + 1 : 1;

            if (_strategy != null && !_strategy.CanProduceBlock(nextBlockNumber))
                throw new SequencerLeaseNotHeldException(nextBlockNumber);

            var options = _strategy != null
                ? _strategy.PrepareBlockOptions(nextBlockNumber, parentHeader)
                : _defaultStrategy.PrepareBlockOptions(nextBlockNumber, parentHeader);

            // EIP-7843. The sequencer is this chain's consensus layer, so it supplies the
            // slot as it supplies every other block field. Set after the options are built
            // so BOTH sources are covered — a strategy that already chose a slot keeps it,
            // and the default path gets one. AppChain has no slot semantics of its own:
            // this is a carried number, and nothing reads meaning into it.
            //
            // Gated on the same predicate as the block access list hash, because EIP-7843
            // and EIP-7928 are emitted together or not at all: a pre-Amsterdam header
            // carrying a slot is a shape no fork produces, and BlockHeaderCodecSelector
            // refuses to encode it.
            if (CarriesAmsterdamHeaderFieldsAt(nextBlockNumber, (ulong)options.Timestamp) && options.SlotNumber == null)
                options.SlotNumber = (ulong)nextBlockNumber;

            var result = await _coreBlockProducer.ProduceBlockAsync(transactions, options);

            if (_strategy != null)
            {
                await _strategy.FinalizeBlockAsync(result.Header, result.BlockHash, result);
            }

            return result;
        }
    }
}
