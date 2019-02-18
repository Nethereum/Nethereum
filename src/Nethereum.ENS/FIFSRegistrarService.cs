using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.FIFSRegistrar.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class FIFSRegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, FIFSRegistrarDeployment fIFSRegistrarDeployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<FIFSRegistrarDeployment>().SendRequestAndWaitForReceiptAsync(fIFSRegistrarDeployment, token);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, FIFSRegistrarDeployment fIFSRegistrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<FIFSRegistrarDeployment>().SendRequestAsync(fIFSRegistrarDeployment);
        }
        public static async Task<FIFSRegistrarService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, FIFSRegistrarDeployment fIFSRegistrarDeployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, fIFSRegistrarDeployment, token);
            return new FIFSRegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public FIFSRegistrarService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, token);
        }

        public Task<string> RegisterRequestAsync(byte[] subnode, string owner)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Subnode = subnode;
                registerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(byte[] subnode, string owner, CancellationToken token = default(CancellationToken))
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Subnode = subnode;
                registerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, token);
        }
    }
}
