using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet;
using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondLoupeFacet;

namespace Nethereum.Contracts.Standards.ERC2535Diamond
{
    public class ERC2535DiamondService
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC2535DiamondService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC2535DiamondCutFacetContractService GetDiamondCutFacetContractService(string contractAddress)
        {
            return new ERC2535DiamondCutFacetContractService(_ethApiContractService, contractAddress);
        }

        public ERC2535DiamondLoupeFacetContractService GetDiamondLoupeFacetService(string contractAddress)
        {
            return new ERC2535DiamondLoupeFacetContractService(_ethApiContractService, contractAddress);
        }
    }
}
