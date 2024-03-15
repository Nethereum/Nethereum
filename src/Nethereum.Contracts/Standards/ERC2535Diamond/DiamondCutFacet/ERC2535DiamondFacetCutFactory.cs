using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet.ContractDefinition;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet;

namespace Nethereum.Contracts //Keep this namespace to surface the extension methods to all the contracts
{
    public static class ERC2535DiamondFacetCutFactory
    {
        public static FacetCut CreateAddFacetCut(string address, params string[] signatures)
        {
            return new FacetCut()
            {
                Action = (byte)FacetCutAction.Add,
                FacetAddress = address,
                FunctionSelectors = signatures.Select(x => x.HexToByteArray()).ToList()
            };
        }

        public static FacetCut CreateReplaceFacetCut(string address, params string[] signatures)
        {
            return new FacetCut()
            {
                Action = (byte)FacetCutAction.Replace,
                FacetAddress = address,
                FunctionSelectors = signatures.Select(x => x.HexToByteArray()).ToList()
            };
        }

        public static FacetCut CreateRemoveFacetCut(string address, params string[] signatures)
        {
            return new FacetCut()
            {
                Action = (byte)FacetCutAction.Remove,
                FacetAddress = address,
                FunctionSelectors = signatures.Select(x => x.HexToByteArray()).ToList()
            };
        }

        public static FacetCut CreateAddFacetCut(string address, List<string> signatures)
        {
            return CreateAddFacetCut(address, signatures.ToArray());
        }
        
        public static FacetCut CreateReplaceFacetCut(string address, List<string> signatures)
        {
            return CreateReplaceFacetCut(address, signatures.ToArray());
        }

        public static FacetCut CreateRemoveFacetCut(string address, List<string> signatures)
        {
            return CreateRemoveFacetCut(address, signatures.ToArray());
        }

        public static FacetCut CreateDiamondFacetCutToAddAllFunctionSignatures(this ContractServiceBase contractService)
        {
            return CreateAddFacetCut(contractService.ContractAddress, contractService.GetAllFunctionSignatures());
        }

        public static FacetCut CreateDiamondFacetCutToRemoveAllFunctionSignatures(this ContractServiceBase contractService)
        {
            return CreateRemoveFacetCut(contractService.ContractAddress, contractService.GetAllFunctionSignatures());
        }
    }
}
