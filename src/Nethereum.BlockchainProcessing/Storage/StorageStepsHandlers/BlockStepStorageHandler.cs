using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.Storage.Repositories;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.StorageStepsHandlers
{
    public class BlockStepStorageHandler : ProcessorBaseHandler<Block>
    {
        private readonly IBlockRepository _blockRepository;

        public BlockStepStorageHandler(IBlockRepository blockRepository)
        {
            _blockRepository = blockRepository;
        }
        protected override Task ExecuteInternalAsync(Block block)
        {
            return _blockRepository.UpsertBlockAsync(block);
        }
    }
}
