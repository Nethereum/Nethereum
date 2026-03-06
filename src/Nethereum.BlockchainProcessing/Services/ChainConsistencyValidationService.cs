using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services
{
    public class ChainConsistencyValidationService
    {
        private readonly IEthApiContractService _ethApi;
        private readonly IChainStateRepository _chainStateRepository;

        public int ReorgBuffer { get; set; } = 0;

        public ChainConsistencyValidationService(
            IEthApiContractService ethApi,
            IChainStateRepository chainStateRepository)
        {
            _ethApi = ethApi ?? throw new ArgumentNullException(nameof(ethApi));
            _chainStateRepository = chainStateRepository ?? throw new ArgumentNullException(nameof(chainStateRepository));
        }

        public async Task ValidateAsync(CancellationToken cancellationToken)
        {
            var state = await _chainStateRepository.GetChainStateAsync().ConfigureAwait(false);
            if (state == null || state.LastCanonicalBlockNumber == null
                || string.IsNullOrWhiteSpace(state.LastCanonicalBlockHash))
                return;

            var lastCanonicalNumber = new BigInteger(state.LastCanonicalBlockNumber.Value);

            var rpcBlock = await _ethApi.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new HexBigInteger(lastCanonicalNumber))
                .ConfigureAwait(false);

            if (rpcBlock != null && HashesEqual(rpcBlock.BlockHash, state.LastCanonicalBlockHash))
                return;

            var chainHead = await _ethApi.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);

            var rewindTo = lastCanonicalNumber - ReorgBuffer;
            if (chainHead.Value < lastCanonicalNumber)
                rewindTo = BigInteger.Min(chainHead.Value, rewindTo);
            if (rewindTo < 0) rewindTo = 0;

            await RewindChainStateAsync(state, rewindTo).ConfigureAwait(false);

            BlockWithTransactions currentBlock;
            if (rpcBlock != null)
            {
                currentBlock = rpcBlock;
            }
            else
            {
                currentBlock = await _ethApi.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(chainHead)
                    .ConfigureAwait(false) ?? new BlockWithTransactions();
            }

            throw new ReorgDetectedException(
                rewindTo, lastCanonicalNumber,
                state.LastCanonicalBlockHash ?? string.Empty,
                currentBlock.Number?.Value ?? 0,
                currentBlock.BlockHash ?? string.Empty,
                currentBlock.ParentHash ?? string.Empty);
        }

        public async Task UpdateChainStateAsync(BigInteger blockNumber)
        {
            var block = await _ethApi.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber))
                .ConfigureAwait(false);
            if (block == null) return;

            var state = await _chainStateRepository.GetChainStateAsync().ConfigureAwait(false) ?? new ChainState();
            state.LastCanonicalBlockNumber = (long)(block.Number?.Value ?? 0);
            state.LastCanonicalBlockHash = block.BlockHash ?? string.Empty;
            await _chainStateRepository.UpsertChainStateAsync(state).ConfigureAwait(false);
        }

        private async Task RewindChainStateAsync(ChainState state, BigInteger rewindTo)
        {
            if (rewindTo > 0)
            {
                var prevBlock = await _ethApi.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(new HexBigInteger(rewindTo - 1))
                    .ConfigureAwait(false);
                if (prevBlock != null)
                {
                    state.LastCanonicalBlockNumber = (long)(prevBlock.Number?.Value ?? 0);
                    state.LastCanonicalBlockHash = prevBlock.BlockHash ?? string.Empty;
                }
                else
                {
                    state.LastCanonicalBlockNumber = null;
                    state.LastCanonicalBlockHash = null;
                }
            }
            else
            {
                state.LastCanonicalBlockNumber = null;
                state.LastCanonicalBlockHash = null;
            }
            await _chainStateRepository.UpsertChainStateAsync(state).ConfigureAwait(false);
        }

        private static bool HashesEqual(string left, string right) =>
            string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
