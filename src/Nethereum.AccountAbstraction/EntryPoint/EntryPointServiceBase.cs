using Nethereum.Contracts.Services;
using Nethereum.RPC;

namespace Nethereum.AccountAbstraction.EntryPoint
{
    public partial class EntryPointServiceBase
    {
        protected IEthApiContractService EthApiContractServiceInternal { get; private set; }

        protected IEthApiContractService EthApi => EthApiContractServiceInternal ?? Web3?.Eth;

        public EntryPointServiceBase(IEthApiContractService ethApiContractService, string contractAddress)
            : base(ethApiContractService, contractAddress)
        {
            EthApiContractServiceInternal = ethApiContractService;
        }
    }
}
