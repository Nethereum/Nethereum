using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondLoupeFacet.ContractDefinition;
using System;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;


namespace Nethereum.Contracts.Standards.ERC2535Diamond.DiamondLoupeFacet
{
    public partial class DiamondLoupeFacetContractService : ContractServiceBase
    {

        public DiamondLoupeFacetContractService(IEthApiContractService ethApiContractService, string contractAddress)
        {
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

#if !DOTNET35
        public Task<string> FacetAddressQueryAsync(FacetAddressFunction facetAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FacetAddressFunction, string>(facetAddressFunction, blockParameter);
        }

        
        public Task<string> FacetAddressQueryAsync(byte[] functionSelector, BlockParameter blockParameter = null)
        {
            var facetAddressFunction = new FacetAddressFunction();
                facetAddressFunction.FunctionSelector = functionSelector;
            
            return ContractHandler.QueryAsync<FacetAddressFunction, string>(facetAddressFunction, blockParameter);
        }

        public Task<List<string>> FacetAddressesQueryAsync(FacetAddressesFunction facetAddressesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FacetAddressesFunction, List<string>>(facetAddressesFunction, blockParameter);
        }

        
        public Task<List<string>> FacetAddressesQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FacetAddressesFunction, List<string>>(null, blockParameter);
        }

        public Task<List<byte[]>> FacetFunctionSelectorsQueryAsync(FacetFunctionSelectorsFunction facetFunctionSelectorsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FacetFunctionSelectorsFunction, List<byte[]>>(facetFunctionSelectorsFunction, blockParameter);
        }

        
        public Task<List<byte[]>> FacetFunctionSelectorsQueryAsync(string facet, BlockParameter blockParameter = null)
        {
            var facetFunctionSelectorsFunction = new FacetFunctionSelectorsFunction();
                facetFunctionSelectorsFunction.Facet = facet;
            
            return ContractHandler.QueryAsync<FacetFunctionSelectorsFunction, List<byte[]>>(facetFunctionSelectorsFunction, blockParameter);
        }

        public Task<FacetsOutputDTO> FacetsQueryAsync(FacetsFunction facetsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<FacetsFunction, FacetsOutputDTO>(facetsFunction, blockParameter);
        }

        public Task<FacetsOutputDTO> FacetsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<FacetsFunction, FacetsOutputDTO>(null, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }
#endif

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(FacetAddressFunction),
                typeof(FacetAddressesFunction),
                typeof(FacetFunctionSelectorsFunction),
                typeof(FacetsFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>();
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>();
        }

    }
}
