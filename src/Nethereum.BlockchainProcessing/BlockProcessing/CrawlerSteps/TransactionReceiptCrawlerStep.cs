using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class TransactionReceiptCrawlerStep : CrawlerStep<TransactionVO, TransactionReceiptVO>
    {
        public TransactionReceiptCrawlerStep(IEthApiContractService ethApiContractService) : base(ethApiContractService)
        {
        }

        public override async Task<TransactionReceiptVO> GetStepDataAsync(TransactionVO transactionVO)
        {
            var receipt = await EthApi.Transactions
                .GetTransactionReceipt.SendRequestAsync(transactionVO.Transaction.TransactionHash)
                .ConfigureAwait(false);
            return new TransactionReceiptVO(transactionVO.Block, transactionVO.Transaction, receipt, receipt.HasErrors()?? false);
        }

        public async Task<CrawlerStepCompleted<TransactionReceiptVO>> ExecuteStepAsync(
            TransactionReceiptVO preBuiltReceiptVO,
            IEnumerable<BlockProcessingSteps> executionStepsCollection)
        {
            if (!Enabled) return null;
            if (preBuiltReceiptVO == null) return null;

            var stepsToProcess =
                await executionStepsCollection.FilterMatchingStepAsync(preBuiltReceiptVO).ConfigureAwait(false);

            if (stepsToProcess.Any())
            {
                await stepsToProcess.ExecuteCurrentStepAsync(preBuiltReceiptVO).ConfigureAwait(false);
            }

            return new CrawlerStepCompleted<TransactionReceiptVO>(stepsToProcess, preBuiltReceiptVO);
        }
    }
}