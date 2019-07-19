using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.BlockStorageStepsHandlers
{
    public class ContractCreationStorageStepHandler : ProcessorBaseHandler<ContractCreationVO>
    {
        private readonly IContractRepository _contractRepository;
        public ContractCreationStorageStepHandler(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }
        protected override Task ExecuteInternalAsync(ContractCreationVO contractCreation)
        {
            return _contractRepository.UpsertAsync(
                contractCreation);
        }
    }
}
