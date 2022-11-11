using Nethereum.ABI.Model;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using System.Collections.Generic;
using System.Linq;

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

        public List<FunctionABI> GetRequiredFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<BalanceOfFunction>(),
                ABITypedRegistry.GetFunctionABI<BalanceOfBatchFunction>(),
                ABITypedRegistry.GetFunctionABI<SafeTransferFromFunction>(),
                ABITypedRegistry.GetFunctionABI<SafeBatchTransferFromFunction>(),
                ABITypedRegistry.GetFunctionABI<SetApprovalForAllFunction>(),
                ABITypedRegistry.GetFunctionABI<IsApprovedForAllFunction>(),
              
            };
            return signatures;
        }

        public string[] GetRequiredFunctionSignatures()
        {
            return GetRequiredFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

        public List<FunctionABI> GetOptionalMetadataExtensionsFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<UriFunction>(),
            };
            return signatures;
        }

        public List<FunctionABI> GetOptionalEnumerableExtensionFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<TotalSupplyFunction>(),
            };
            return signatures;
        }

        public string[] GetOptionalMetadataFunctionSignatures()
        {
            return GetOptionalMetadataExtensionsFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

        public string[] GetOptionalEnumerableExtensionFunctionSignatures()
        {
            return GetOptionalEnumerableExtensionFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

    }
}
