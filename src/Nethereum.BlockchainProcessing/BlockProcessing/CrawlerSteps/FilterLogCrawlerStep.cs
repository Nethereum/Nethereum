using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class FilterLogCrawlerStep : CrawlerStep<FilterLogVO, FilterLogVO>
    {
        public FilterLogCrawlerStep(IEthApiContractService ethApiContractService) : base(ethApiContractService)
        {
        }

        public override Task<FilterLogVO> GetStepDataAsync(FilterLogVO filterLogVO)
        {
            return Task.FromResult(filterLogVO);
        }
    }
}