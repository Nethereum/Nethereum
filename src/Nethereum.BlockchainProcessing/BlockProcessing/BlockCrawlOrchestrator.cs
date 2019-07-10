using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{

    public class BlockCrawlOrchestrator: IBlockchainProcessingOrchestrator
    {
        protected IEthApiContractService EthApi { get; set; }
        public IEnumerable<BlockchainProcessorExecutionSteps> ExecutionStepsCollection { get; }
        protected BlockCrawlerStep BlockCrawlerStep { get; }
        protected TransactionCrawlerStep TransactionWithBlockCrawlerStep { get; }
        protected TransactionReceiptCrawlerStep TransactionWithReceiptCrawlerStep { get; }
        protected ContractCreatedCrawlerStep ContractCreatedCrawlerStep { get; }

        public BlockCrawlOrchestrator(IEthApiContractService ethApi, IEnumerable<BlockchainProcessorExecutionSteps> executionStepsCollection)
        {
            
            this.ExecutionStepsCollection = executionStepsCollection;
            EthApi = ethApi;
            BlockCrawlerStep = new BlockCrawlerStep(ethApi);
            TransactionWithBlockCrawlerStep = new TransactionCrawlerStep(ethApi);
            TransactionWithReceiptCrawlerStep = new TransactionReceiptCrawlerStep(ethApi);
            ContractCreatedCrawlerStep = new ContractCreatedCrawlerStep(ethApi);
        }

        public virtual async Task CrawlBlock(BigInteger blockNumber)
        {
            var blockCrawlerStepCompleted = await BlockCrawlerStep.ExecuteStepAsync(blockNumber, ExecutionStepsCollection);
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
            await CrawlTransactionReceipt(currentStepCompleted);
        }

        protected virtual async Task CrawlTransactionReceipt(CrawlerStepCompleted<TransactionVO> completedStep)
        {
           var currentStepCompleted = await TransactionWithReceiptCrawlerStep.ExecuteStepAsync(completedStep.StepData,
                completedStep.ExecutedStepsCollection);
            if(currentStepCompleted != null && currentStepCompleted.StepData.IsForContractCreation())
            {
                await ContractCreatedCrawlerStep.ExecuteStepAsync(currentStepCompleted.StepData, completedStep.ExecutedStepsCollection);
            }
        }

        public async Task<OrchestrationProgress> ProcessAsync(BigInteger fromNumber, BigInteger toNumber)
        {
            var progress = new OrchestrationProgress();
            try
            {
                var currentBlockNumber = fromNumber;
                while (currentBlockNumber <= toNumber)
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