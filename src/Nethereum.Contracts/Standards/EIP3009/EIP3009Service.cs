using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.EIP3009.ContractDefinition;

namespace Nethereum.Contracts.Standards.EIP3009
{
    /// <summary>
    /// EIP-3009: Transfer With Authorization Service
    /// Service factory to interact with smart contracts implementing the EIP-3009 standard
    /// https://eips.ethereum.org/EIPS/eip-3009
    /// </summary>
    public class EIP3009Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public EIP3009Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public EIP3009ContractService GetContractService(string contractAddress)
        {
            return new EIP3009ContractService(_ethApiContractService, contractAddress);
        }

        /// <summary>
        /// Get required EIP-3009 function ABIs
        /// </summary>
        public List<FunctionABI> GetRequiredFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<TransferWithAuthorizationFunction>(),
                ABITypedRegistry.GetFunctionABI<AuthorizationStateFunction>()
            };
            return signatures;
        }

        public string[] GetRequiredFunctionSignatures()
        {
            return GetRequiredFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

        /// <summary>
        /// Get optional EIP-3009 function ABIs (receiveWithAuthorization, cancelAuthorization)
        /// </summary>
        public List<FunctionABI> GetOptionalFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<ReceiveWithAuthorizationFunction>(),
                ABITypedRegistry.GetFunctionABI<CancelAuthorizationFunction>()
            };
            return signatures;
        }

        public string[] GetOptionalFunctionSignatures()
        {
            return GetOptionalFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }
    }
}
