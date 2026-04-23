using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
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
            _coreBlockProducer = new CoreChain.BlockProducer(
                appChain.Blocks,
                appChain.Transactions,
                appChain.Receipts,
                appChain.Logs,
                appChain.State,
                transactionProcessor,
                appChain.TrieNodes,
                stateRootCalculator,
                blockHashProvider,
                blockEncodingProvider,
                blockRootsProvider);
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
