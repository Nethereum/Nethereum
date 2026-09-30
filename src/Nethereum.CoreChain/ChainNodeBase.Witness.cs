using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<byte[]> CaptureBlockWitnessAsync(long blockNumber)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block == null)
                throw new InvalidOperationException($"Block {blockNumber} not found");

            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);

            IStateStore stateForCapture;
            if (_stateStore is IHistoricalStateProvider histProvider)
            {
                var parentBlock = blockNumber > 0 ? blockNumber - 1 : 0;
                stateForCapture = new StateLayer().Stores.WitnessCapture(_stateStore, new HistoricalStateStoreReadAdapter(histProvider, _stateStore, parentBlock));
            }
            else
            {
                stateForCapture = new StateLayer().Stores.WitnessCapture(_stateStore);
            }

            var trieNodeStoreForCapture = _trieNodeStore != null
                ? (ITrieNodeStore)new ReadOnlyTrieNodeStoreWrapper(_trieNodeStore)
                : new InMemoryContentNodeStore();

            var activations = Config.ResolveActivations();

            var forkStampedConfig = ForkStampedConfigFor(block);

            var calculator = new IncrementalStateRootCalculator(stateForCapture, trieNodeStoreForCapture);
            var engine = new BlockExecutor(
                stateForCapture,
                _blockStore,
                activations,
                chainConfigFactory: _ => forkStampedConfig,
                hardforkConfigFactory: Config.ConfigForFork,
                stateRootCalculator: calculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStoreForCapture);

            var blockTxs = await _transactionStore.GetByBlockHashAsync(blockHash);
            var txEntries = new List<TxEntry>(blockTxs?.Count ?? 0);
            if (blockTxs != null)
            {
                foreach (var tx in blockTxs)
                {
                    var sender = _txVerifier.GetSenderAddress(tx);
                    txEntries.Add(new TxEntry(tx, sender));
                }
            }

            var result = await engine.ExecuteAsync(
                block,
                txEntries,
                uncles: null,
                withdrawals: null,
                new BlockExecutionOptions
                {
                    ReadOnly = true,
                    CaptureWitness = true,
                    ParentBeaconBlockRoot = block.ParentBeaconBlockRoot
                });

            return result.WitnessBytes ?? Array.Empty<byte>();
        }
    }
}
