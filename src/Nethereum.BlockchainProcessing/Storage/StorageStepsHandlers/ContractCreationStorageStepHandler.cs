using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.Storage.Repositories;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.StorageStepsHandlers
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
