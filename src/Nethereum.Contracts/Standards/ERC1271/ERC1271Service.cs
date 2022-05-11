using Nethereum.Contracts.Services;

namespace Nethereum.Contracts.Standards.ERC1271
{
    public class ERC1271Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC1271Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC1271ContractService GetContractService(string contractAddress)
        {
            return new ERC1271ContractService(_ethApiContractService, contractAddress);
        }

    }


}
