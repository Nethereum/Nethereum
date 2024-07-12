using Nethereum.Contracts.Services;

namespace Nethereum.Contracts.Standards.ERC165
{
    public class ERC165SupportsInterfaceService
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC165SupportsInterfaceService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC165SupportsInterfaceContractService GetContractService(string contractAddress)
        {
            return new ERC165SupportsInterfaceContractService(_ethApiContractService, contractAddress);
        }
    }
}
