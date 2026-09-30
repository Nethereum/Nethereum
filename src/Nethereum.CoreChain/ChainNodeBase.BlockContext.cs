using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        protected virtual async Task<BlockContext> GetBlockContextForCallAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            return new BlockContext
            {
                BlockNumber = latestBlock?.BlockNumber ?? 0,
                Timestamp = latestBlock?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = Config.Coinbase,
                GasLimit = Config.BlockGasLimit,
                BaseFee = Config.BaseFee,
                ChainId = Config.ChainId,
                Difficulty = 0,
                SlotNumber = latestBlock?.SlotNumber
            };
        }

        protected virtual async Task<IStateReader> GetNodeDataServiceAtBlockAsync(BigInteger blockNumber)
        {
            if (_stateStore is IHistoricalStateProvider historyProvider)
            {
                return new HistoricalNodeDataService(historyProvider, _stateStore, _blockStore, blockNumber);
            }

            var head = await _blockStore.GetHeightAsync().ConfigureAwait(false);
            if (blockNumber < head)
                throw new HistoricalStateNotAvailableException(blockNumber, head);
            return _nodeDataService;
        }

        protected virtual async Task<BlockContext> GetBlockContextAtBlockAsync(BigInteger blockNumber)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block != null)
            {
                return new BlockContext
                {
                    BlockNumber = block.BlockNumber,
                    Timestamp = block.Timestamp,
                    Coinbase = block.Coinbase ?? Config.Coinbase,
                    GasLimit = block.GasLimit,
                    BaseFee = block.BaseFee ?? Config.BaseFee,
                    ChainId = Config.ChainId,
                    Difficulty = block.Difficulty,
                    SlotNumber = block.SlotNumber
                };
            }
            return await GetBlockContextForCallAsync();
        }
    }
}
