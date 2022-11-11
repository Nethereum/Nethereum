using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class TransactionCrawlerStep : CrawlerStep<TransactionVO, TransactionVO>
    {
        public TransactionCrawlerStep(IEthApiContractService ethApiContractService) : base(ethApiContractService)
        {
        }

        public override Task<TransactionVO> GetStepDataAsync(TransactionVO parentStep)
        {
            return Task.FromResult(parentStep);
        }
    }
}