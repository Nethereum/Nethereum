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
            var trieNodeStore = appChain.TrieNodes ?? new InMemoryContentNodeStore();
            var resolvedCalculator = stateRootCalculator
                ?? new IncrementalStateRootCalculator(appChain.State, trieNodeStore);

            var activations = _config.ResolveActivations();
            var engine = new BlockExecutor(
                appChain.State,
                appChain.Blocks,
                activations,
                chainConfigFactory: _ => _config,
                hardforkConfigFactory: _config.ConfigForFork,
                stateRootCalculator: resolvedCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                authorResolver: strategy != null ? strategy.ResolveFeeRecipient : null);

            _coreBlockProducer = new CoreChain.BlockProducer(
                engine,
                appChain.Blocks,
                appChain.Transactions,
                appChain.Receipts,
                appChain.Logs,
                appChain.State,
                trieNodeStore,
                resolvedCalculator,
                orderingPolicy: MempoolNonceOrderingPolicy.Instance,
                blockHashProvider: blockHashProvider,
                blockEncodingProvider: blockEncodingProvider,
                blockRootsProvider: blockRootsProvider,
                withdrawalStore: withdrawalStore,
                nodeCommitBlockContext: null,
                blockAccessListStore: blockAccessListStore,
                hardforkConfigFactory: _config.ConfigForFork);
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
                : CreateDefaultBlockProductionOptions();

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

        private BlockProductionOptions CreateDefaultBlockProductionOptions()
        {
            return new BlockProductionOptions
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = _config.Coinbase,
                BaseFee = _config.BaseFee,
                BlockGasLimit = _config.BlockGasLimit,
                ChainId = _config.ChainId,
                Difficulty = 0,
                ExtraData = System.Text.Encoding.UTF8.GetBytes($"AppChain:{_config.AppChainName}")
            };
        }
    }
}
