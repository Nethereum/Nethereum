using Nethereum.Contracts;
using Nethereum.Contracts.Services;

namespace Nethereum.Web3
{
    public abstract partial class ContractWeb3ServiceBase
    {
        protected ContractWeb3ServiceBase(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
        }
    }
}
