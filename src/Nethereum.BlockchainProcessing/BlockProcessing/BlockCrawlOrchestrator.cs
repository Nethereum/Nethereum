using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using System.Linq;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{

    public class BlockCrawlOrchestrator: IBlockchainProcessingOrchestrator
    {
        public IEthApiContractService EthApi { get; set; }
        public IEnumerable<BlockProcessingSteps> ProcessingStepsCollection { get; }
        public BlockCrawlerStep BlockCrawlerStep { get; }
        public TransactionCrawlerStep TransactionWithBlockCrawlerStep { get; }
        public TransactionReceiptCrawlerStep TransactionWithReceiptCrawlerStep { get; }
        public ContractCreatedCrawlerStep ContractCreatedCrawlerStep { get; }

        public FilterLogCrawlerStep FilterLogCrawlerStep { get; }
        public IChainStateRepository ChainStateRepository { get; set; }
        public INonCanonicalBlockRepository NonCanonicalBlockRepository { get; set; }
        public INonCanonicalTransactionRepository NonCanonicalTransactionRepository { get; set; }
        public INonCanonicalTransactionLogRepository NonCanonicalTransactionLogRepository { get; set; }
        public INonCanonicalTokenTransferLogRepository NonCanonicalTokenTransferLogRepository { get; set; }
        public INonCanonicalInternalTransactionRepository NonCanonicalInternalTransactionRepository { get; set; }
        public ITokenBalanceRepository TokenBalanceRepository { get; set; }
        public INFTInventoryRepository NFTInventoryRepository { get; set; }
        public IReorgHandler ReorgHandler { get; set; }
        public BigInteger ReorgBuffer { get; set; } = 0;
        public bool UseBatchReceipts { get; set; }
        private Dictionary<string, TransactionReceipt> _batchedReceipts;

        public BlockCrawlOrchestrator(IEthApiContractService ethApi, BlockProcessingSteps blockProcessingSteps)
            :this(ethApi, new[] { blockProcessingSteps })
        {

        }

        public BlockCrawlOrchestrator(IEthApiContractService ethApi, IEnumerable<BlockProcessingSteps> processingStepsCollection)
        {
            
            this.ProcessingStepsCollection = processingStepsCollection;
            EthApi = ethApi;
            BlockCrawlerStep = new BlockCrawlerStep(ethApi);
            TransactionWithBlockCrawlerStep = new TransactionCrawlerStep(ethApi);
            TransactionWithReceiptCrawlerStep = new TransactionReceiptCrawlerStep(ethApi);
            ContractCreatedCrawlerStep = new ContractCreatedCrawlerStep(ethApi);
            FilterLogCrawlerStep = new FilterLogCrawlerStep(ethApi);
        }

        public virtual async Task CrawlBlockAsync(BigInteger blockNumber)
        {
            var blockCrawlerStepCompleted = await BlockCrawlerStep.ExecuteStepAsync(blockNumber, ProcessingStepsCollection).ConfigureAwait(false);
            if (blockCrawlerStepCompleted?.StepData != null)
            {
                await ValidateCanonicalAsync(blockCrawlerStepCompleted.StepData).ConfigureAwait(false);
            }

            if (UseBatchReceipts && TransactionWithReceiptCrawlerStep.Enabled)
            {
                await PreFetchBlockReceiptsAsync(blockNumber).ConfigureAwait(false);
            }

            await CrawlTransactionsAsync(blockCrawlerStepCompleted).ConfigureAwait(false);

            _batchedReceipts = null;

            if (blockCrawlerStepCompleted?.StepData != null)
            {
                await UpdateChainStateAsync(blockCrawlerStepCompleted.StepData).ConfigureAwait(false);
            }
        }
        protected virtual async Task CrawlTransactionsAsync(CrawlerStepCompleted<BlockWithTransactions> completedStep)
        {
            if (completedStep != null)
            {
                foreach (var txn in completedStep.StepData.Transactions)
                {
                    await CrawlTransactionAsync(completedStep, txn).ConfigureAwait(false);
                }
            }
        }
        protected virtual async Task CrawlTransactionAsync(CrawlerStepCompleted<BlockWithTransactions> completedStep, Nethereum.RPC.Eth.DTOs.Transaction txn)
        {
            var currentStepCompleted = await TransactionWithBlockCrawlerStep.ExecuteStepAsync(
                new TransactionVO(txn, completedStep.StepData), completedStep.ExecutedStepsCollection).ConfigureAwait(false);

            if(currentStepCompleted.ExecutedStepsCollection.Any() && TransactionWithReceiptCrawlerStep.Enabled)
            { 
                await CrawlTransactionReceiptAsync(currentStepCompleted).ConfigureAwait(false);
            }
        }

        protected virtual async Task CrawlTransactionReceiptAsync(CrawlerStepCompleted<TransactionVO> completedStep)
        {
            if (TransactionWithReceiptCrawlerStep.Enabled)
            {
                CrawlerStepCompleted<TransactionReceiptVO> currentStepCompleted;

                if (_batchedReceipts != null && _batchedReceipts.TryGetValue(
                        completedStep.StepData.Transaction.TransactionHash, out var cachedReceipt))
                {
                    var receiptVO = new TransactionReceiptVO(
                        completedStep.StepData.Block,
                        completedStep.StepData.Transaction,
                        cachedReceipt,
                        cachedReceipt.HasErrors() ?? false);
                    currentStepCompleted = await TransactionWithReceiptCrawlerStep.ExecuteStepAsync(
                        receiptVO,
                        completedStep.ExecutedStepsCollection).ConfigureAwait(false);
                }
                else
                {
                    currentStepCompleted = await TransactionWithReceiptCrawlerStep.ExecuteStepAsync(
                        completedStep.StepData,
                        completedStep.ExecutedStepsCollection).ConfigureAwait(false);
                }

                if (currentStepCompleted != null && currentStepCompleted.StepData.IsForContractCreation() &&
                    ContractCreatedCrawlerStep.Enabled)
                {
                    await ContractCreatedCrawlerStep.ExecuteStepAsync(currentStepCompleted.StepData,
                        completedStep.ExecutedStepsCollection).ConfigureAwait(false);
                }

                await CrawlFilterLogsAsync(currentStepCompleted).ConfigureAwait(false);
            }
        }


        protected virtual async Task CrawlFilterLogsAsync(CrawlerStepCompleted<TransactionReceiptVO> completedStep)
        {
            if (completedStep != null && FilterLogCrawlerStep.Enabled)
            {
                foreach (var log in completedStep.StepData.TransactionReceipt.Logs.ConvertToFilterLog())
                {
                    await CrawlFilterLogAsync(completedStep, log).ConfigureAwait(false);
                }
            }
        }

        protected virtual async Task CrawlFilterLogAsync(CrawlerStepCompleted<TransactionReceiptVO> completedStep, FilterLog filterLog)
        {
            if (FilterLogCrawlerStep.Enabled)
            {
                var currentStepCompleted = await FilterLogCrawlerStep.ExecuteStepAsync(
                    new FilterLogVO(completedStep.StepData.Transaction, completedStep.StepData.TransactionReceipt,
                        filterLog), completedStep.ExecutedStepsCollection).ConfigureAwait(false);
            }
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber, CancellationToken cancellationToken = default(CancellationToken), IBlockProgressRepository blockProgressRepository = null)
        {
            var progress = new OrchestrationProgress();
            try
            {
                var currentBlockNumber = fromNumber;
                while (currentBlockNumber <= toNumber && !cancellationToken.IsCancellationRequested)
                {

                    await CrawlBlockAsync(currentBlockNumber).ConfigureAwait(false);
                    progress.BlockNumberProcessTo = currentBlockNumber;
                    if (blockProgressRepository != null)
                    {
                        await blockProgressRepository.UpsertProgressAsync(progress.BlockNumberProcessTo.Value).ConfigureAwait(false);
                    }
                    currentBlockNumber = currentBlockNumber + 1;
                }
            }
            catch (Exception ex)
            {
                progress.Exception = ex;
            }

            return progress;
        }

        private async Task ValidateCanonicalAsync(BlockWithTransactions block)
        {
            if (ChainStateRepository == null)
            {
                return;
            }

            var state = await ChainStateRepository.GetChainStateAsync().ConfigureAwait(false);
            if (state == null || state.LastCanonicalBlockNumber == null || string.IsNullOrWhiteSpace(state.LastCanonicalBlockHash))
            {
                return;
            }

            var lastCanonicalNumber = new BigInteger(state.LastCanonicalBlockNumber.Value);
            var currentNumber = block.Number?.Value ?? 0;
            var lastCanonicalHash = state.LastCanonicalBlockHash;
            var currentParentHash = block.ParentHash ?? string.Empty;

            var isNextBlock = currentNumber == lastCanonicalNumber + 1;
            var isReplayAtHead = currentNumber == lastCanonicalNumber;

            if (isNextBlock && !HashesEqual(currentParentHash, lastCanonicalHash))
            {
                await HandleReorgAsync(block, state, lastCanonicalNumber).ConfigureAwait(false);
            }
            else if (isReplayAtHead && !HashesEqual(block.BlockHash, lastCanonicalHash))
            {
                await HandleReorgAsync(block, state, lastCanonicalNumber).ConfigureAwait(false);
            }
        }

        public virtual async Task ValidateChainConsistencyAsync(CancellationToken cancellationToken)
        {
            if (ChainStateRepository == null)
                return;

            var state = await ChainStateRepository.GetChainStateAsync().ConfigureAwait(false);
            if (state == null || state.LastCanonicalBlockNumber == null ||
                string.IsNullOrWhiteSpace(state.LastCanonicalBlockHash))
                return;

            var lastCanonicalNumber = new BigInteger(state.LastCanonicalBlockNumber.Value);

            var lastCanonicalRpcBlock = await EthApi.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new HexBigInteger(lastCanonicalNumber))
                .ConfigureAwait(false);

            if (lastCanonicalRpcBlock != null &&
                HashesEqual(lastCanonicalRpcBlock.BlockHash, state.LastCanonicalBlockHash))
                return;

            var chainHead = await EthApi.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);

            var rewindTo = lastCanonicalNumber - ReorgBuffer;
            if (chainHead.Value < lastCanonicalNumber)
                rewindTo = BigInteger.Min(chainHead.Value, rewindTo);

            BlockWithTransactions currentBlock;
            if (lastCanonicalRpcBlock != null)
            {
                currentBlock = lastCanonicalRpcBlock;
            }
            else
            {
                currentBlock = await EthApi.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(chainHead)
                    .ConfigureAwait(false) ?? new BlockWithTransactions();
            }

            await HandleReorgAsync(
                currentBlock,
                state,
                lastCanonicalNumber,
                rewindTo).ConfigureAwait(false);
        }

        private async Task HandleReorgAsync(BlockWithTransactions block, ChainState state, BigInteger lastCanonicalNumber, BigInteger? rewindToOverride = null)
        {
            var rewindTo = rewindToOverride ?? (lastCanonicalNumber - ReorgBuffer);
            if (rewindTo < 0) rewindTo = 0;

            if (ReorgHandler != null)
            {
                await ReorgHandler.MarkBlockRangeNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
            }
            else
            {
                if (NonCanonicalBlockRepository != null)
                {
                    await MarkBlocksNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
                }

                if (NonCanonicalTransactionRepository != null)
                {
                    await MarkTransactionsNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
                }

                if (NonCanonicalTransactionLogRepository != null)
                {
                    await MarkTransactionLogsNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
                }

                if (NonCanonicalInternalTransactionRepository != null)
                {
                    await MarkInternalTransactionsNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
                }
            }

            if (NonCanonicalTokenTransferLogRepository != null)
            {
                await MarkTokenTransferLogsNonCanonicalAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
            }

            if (TokenBalanceRepository != null)
            {
                await DeleteTokenBalancesAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
            }

            if (NFTInventoryRepository != null)
            {
                await DeleteNFTInventoryAsync(rewindTo, lastCanonicalNumber).ConfigureAwait(false);
            }

            var oldCanonicalHash = state.LastCanonicalBlockHash ?? string.Empty;

            if (ChainStateRepository != null)
            {
                if (rewindTo > 0)
                {
                    var previousBlock = await EthApi.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(new HexBigInteger(rewindTo - 1))
                        .ConfigureAwait(false);

                    if (previousBlock != null)
                    {
                        state.LastCanonicalBlockNumber = (long)(previousBlock.Number?.Value ?? 0);
                        state.LastCanonicalBlockHash = previousBlock.BlockHash ?? string.Empty;
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

                await ChainStateRepository.UpsertChainStateAsync(state).ConfigureAwait(false);
            }

            throw new ReorgDetectedException(
                rewindTo,
                lastCanonicalNumber,
                oldCanonicalHash,
                block.Number?.Value ?? 0,
                block.BlockHash ?? string.Empty,
                block.ParentHash ?? string.Empty);
        }

        private async Task UpdateChainStateAsync(BlockWithTransactions block)
        {
            if (ChainStateRepository == null)
            {
                return;
            }

            var state = await ChainStateRepository.GetChainStateAsync().ConfigureAwait(false) ?? new ChainState();
            state.LastCanonicalBlockNumber = (long)(block.Number?.Value ?? 0);
            state.LastCanonicalBlockHash = block.BlockHash ?? string.Empty;
            await ChainStateRepository.UpsertChainStateAsync(state).ConfigureAwait(false);
        }

        private static bool HashesEqual(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private async Task MarkBlocksNonCanonicalAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NonCanonicalBlockRepository.MarkNonCanonicalAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task MarkTransactionsNonCanonicalAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NonCanonicalTransactionRepository.MarkNonCanonicalAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task MarkTransactionLogsNonCanonicalAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NonCanonicalTransactionLogRepository.MarkNonCanonicalAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task MarkTokenTransferLogsNonCanonicalAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NonCanonicalTokenTransferLogRepository.MarkNonCanonicalAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task MarkInternalTransactionsNonCanonicalAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NonCanonicalInternalTransactionRepository.MarkNonCanonicalAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task DeleteTokenBalancesAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await TokenBalanceRepository.DeleteByBlockNumberAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task DeleteNFTInventoryAsync(BigInteger fromBlockNumber, BigInteger toBlockNumber)
        {
            for (var blockNumber = fromBlockNumber; blockNumber <= toBlockNumber; blockNumber++)
            {
                await NFTInventoryRepository.DeleteByBlockNumberAsync(blockNumber).ConfigureAwait(false);
            }
        }

        private async Task PreFetchBlockReceiptsAsync(BigInteger blockNumber)
        {
            var receipts = await EthApi.Blocks.GetBlockReceiptsByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber))
                .ConfigureAwait(false);

            _batchedReceipts = new Dictionary<string, TransactionReceipt>(StringComparer.OrdinalIgnoreCase);

            if (receipts != null)
            {
                foreach (var receipt in receipts)
                {
                    if (receipt?.TransactionHash != null)
                    {
                        _batchedReceipts[receipt.TransactionHash] = receipt;
                    }
                }
            }
        }
    }
}
