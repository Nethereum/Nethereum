using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.BlockchainProcessing.Services
{
    public class BlockchainProcessingService : IBlockchainProcessingService
    {
        private readonly IEthApiContractService _ethApiContractService;
        public IBlockchainLogProcessingService Logs { get; }
        public IBlockchainBlockProcessingService Blocks { get; }
    
        public BlockchainProcessingService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
            Logs = new BlockchainLogProcessingService(ethApiContractService);
            Blocks = new BlockchainBlockProcessingService(ethApiContractService);
        }

    }
}
