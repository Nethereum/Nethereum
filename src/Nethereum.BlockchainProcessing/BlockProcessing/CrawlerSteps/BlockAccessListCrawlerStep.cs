using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockProcessing.CrawlerSteps
{
    public class BlockAccessListCrawlerStep : CrawlerStep<BlockAccessListVO, BlockAccessListVO>
    {
        public BlockAccessListCrawlerStep(IEthApiContractService ethApiContractService) : base(ethApiContractService)
        {
            Enabled = false;
        }

        public override Task<BlockAccessListVO> GetStepDataAsync(BlockAccessListVO parentStep)
        {
            return Task.FromResult(parentStep);
        }
    }
}
