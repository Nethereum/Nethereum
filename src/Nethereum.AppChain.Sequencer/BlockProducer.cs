using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

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
            IBlockRootsProvider? blockRootsProvider = null)
        {
            if (appChain == null) throw new ArgumentNullException(nameof(appChain));
            if (transactionProcessor == null) throw new ArgumentNullException(nameof(transactionProcessor));

            _appChain = appChain;
            _config = appChain.Config;
            _strategy = strategy;
            var trieNodeStore = appChain.TrieNodes ?? new InMemoryTrieNodeStore();
            var resolvedCalculator = stateRootCalculator
                ?? new IncrementalStateRootCalculator(appChain.State, trieNodeStore);

            // AppChain pins a single hardfork (ChainConfig.Hardfork). No
            // PoW miner rewards — the sequencer is the canonical producer
            // and is paid via tx fees + base-fee burn semantics.
            var pinnedFork = Nethereum.EVM.HardforkNames.Parse(_config.Hardfork ?? "prague");
            var activations = new FixedChainActivations(pinnedFork);
            var hardforkConfig = _config.GetHardforkConfig();
            var engine = new BlockExecutor(
                appChain.State,
                appChain.Blocks,
                activations,
                chainConfigFactory: _ => _config,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: resolvedCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

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
                blockRootsProvider: blockRootsProvider);
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(IReadOnlyList<ISignedTransaction> transactions)
        {
            var parentHeader = await _appChain.GetLatestBlockAsync();
            var nextBlockNumber = parentHeader != null ? (long)parentHeader.BlockNumber + 1 : 1;

            var options = _strategy != null
                ? _strategy.PrepareBlockOptions(nextBlockNumber, parentHeader)
                : CreateDefaultBlockProductionOptions();

            var result = await _coreBlockProducer.ProduceBlockAsync(transactions, options);

            if (_strategy != null)
            {
                await _strategy.FinalizeBlockAsync(result.Header, result.BlockHash, result);
            }

            return result;
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
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
