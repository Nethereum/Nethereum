using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class BlockAccessListStorageStepHandler : ProcessorBaseHandler<BlockAccessListVO>
    {
        private readonly IBlockAccessListRepository _blockAccessListRepository;

        public BlockAccessListStorageStepHandler(IBlockAccessListRepository blockAccessListRepository)
        {
            _blockAccessListRepository = blockAccessListRepository;
        }

        protected override async Task ExecuteInternalAsync(BlockAccessListVO blockAccessList)
        {
            if (blockAccessList.Accounts == null) return;

            foreach (var account in blockAccessList.Accounts)
            {
                await _blockAccessListRepository.UpsertAsync(account, blockAccessList.BlockNumber, blockAccessList.BlockHash).ConfigureAwait(false);
            }
        }
    }
}
