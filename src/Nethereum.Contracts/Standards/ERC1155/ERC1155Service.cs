using Nethereum.Contracts.Services;

namespace Nethereum.Contracts.Standards.ERC1155
{
    public class ERC1155Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC1155Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC1155ContractService GetContractService(string contractAddress)
        {
            return new ERC1155ContractService(_ethApiContractService, contractAddress);
        }

    }
}
