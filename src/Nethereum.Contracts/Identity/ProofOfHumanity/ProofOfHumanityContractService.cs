using System.Threading.Tasks;

using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Constants;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using Nethereum.Contracts.Identity.ProofOfHumanity.ContractDefinition;

namespace Nethereum.Contracts.Identity.ProofOfHumanity
{
    public partial class ProofOfHumanityContractService
    {
        private readonly IEthApiContractService _ethApiContractService;
        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public ProofOfHumanityContractService(IEthApiContractService ethApiContractService, string contractAddress = CommonAddresses.PROOF_OF_HUMANITY_PROXY_ADDRESS)
        {
            _ethApiContractService = ethApiContractService;
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }
#if !DOTNET35
        public Task<bool> IsRegisteredQueryAsync(IsRegisteredFunction isRegisteredFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsRegisteredFunction, bool>(isRegisteredFunction, blockParameter);
        }
        
        public Task<bool> IsRegisteredQueryAsync(string submissionID, BlockParameter blockParameter = null)
        {
            var isRegisteredFunction = new IsRegisteredFunction();
                isRegisteredFunction.SubmissionID = submissionID;
            
            return ContractHandler.QueryAsync<IsRegisteredFunction, bool>(isRegisteredFunction, blockParameter);
        }
    
#endif
    }


}
