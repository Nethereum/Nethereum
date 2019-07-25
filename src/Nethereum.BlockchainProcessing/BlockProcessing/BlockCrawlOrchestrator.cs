using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using System.Linq;

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

        public virtual async Task CrawlBlock(BigInteger blockNumber)
        {
            var blockCrawlerStepCompleted = await BlockCrawlerStep.ExecuteStepAsync(blockNumber, ProcessingStepsCollection);
            await CrawlTransactions(blockCrawlerStepCompleted);

        }
        protected virtual async Task CrawlTransactions(CrawlerStepCompleted<BlockWithTransactions> completedStep)
        {
            if (completedStep != null)
            {
                foreach (var txn in completedStep.StepData.Transactions)
                {
                    await CrawlTransaction(completedStep, txn);
                }
            }
        }
        protected virtual async Task CrawlTransaction(CrawlerStepCompleted<BlockWithTransactions> completedStep, Transaction txn)
        {
            var currentStepCompleted = await TransactionWithBlockCrawlerStep.ExecuteStepAsync(
                new TransactionVO(txn, completedStep.StepData), completedStep.ExecutedStepsCollection);

            if(currentStepCompleted.ExecutedStepsCollection.Any())
            { 
                await CrawlTransactionReceipt(currentStepCompleted);
            }
        }

        protected virtual async Task CrawlTransactionReceipt(CrawlerStepCompleted<TransactionVO> completedStep)
        {
           var currentStepCompleted = await TransactionWithReceiptCrawlerStep.ExecuteStepAsync(completedStep.StepData,
                completedStep.ExecutedStepsCollection);
            if(currentStepCompleted != null && currentStepCompleted.StepData.IsForContractCreation())
            {
                await ContractCreatedCrawlerStep.ExecuteStepAsync(currentStepCompleted.StepData, completedStep.ExecutedStepsCollection);
            }

            await CrawlFilterLogs(currentStepCompleted);
        }


        protected virtual async Task CrawlFilterLogs(CrawlerStepCompleted<TransactionReceiptVO> completedStep)
        {
            if (completedStep != null)
            {
                foreach (var log in completedStep.StepData.TransactionReceipt.Logs.ConvertToFilterLog())
                {
                    await CrawlFilterLog(completedStep, log);
                }
            }
        }

        protected virtual async Task CrawlFilterLog(CrawlerStepCompleted<TransactionReceiptVO> completedStep, FilterLog filterLog)
        {
            var currentStepCompleted = await FilterLogCrawlerStep.ExecuteStepAsync(
                new FilterLogVO(completedStep.StepData.Transaction, completedStep.StepData.TransactionReceipt, filterLog), completedStep.ExecutedStepsCollection);
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            var progress = new OrchestrationProgress();
            try
            {
                var currentBlockNumber = fromNumber;
                while (currentBlockNumber <= toNumber && !cancellationToken.IsCancellationRequested)
                {

                    await CrawlBlock(currentBlockNumber);
                    progress.BlockNumberProcessTo = currentBlockNumber;
                    currentBlockNumber = currentBlockNumber + 1;
                }
            }
            catch (Exception ex)
            {
                progress.Exception = ex;
            }

            return progress;
        }
    }
}