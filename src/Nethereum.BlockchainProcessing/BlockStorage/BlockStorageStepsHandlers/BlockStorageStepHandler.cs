using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class BlockStorageStepHandler : ProcessorBaseHandler<BlockWithTransactions>
    {
        private readonly IBlockRepository _blockRepository;

        public BlockStorageStepHandler(IBlockRepository blockRepository)
        {
            _blockRepository = blockRepository;
        }
        protected override Task ExecuteInternalAsync(BlockWithTransactions block)
        {
            return _blockRepository.UpsertBlockAsync(block);
        }
    }
}
