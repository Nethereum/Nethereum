using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.DefaultReverseResolver.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class DefaultReverseResolverService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<DefaultReverseResolverDeployment>().SendRequestAndWaitForReceiptAsync(defaultReverseResolverDeployment, token);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DefaultReverseResolverDeployment>().SendRequestAsync(defaultReverseResolverDeployment);
        }
        public static async Task<DefaultReverseResolverService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, defaultReverseResolverDeployment, token);
            return new DefaultReverseResolverService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public DefaultReverseResolverService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }

        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }



        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var nameFunction = new NameFunction();
                nameFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }



        public Task<string> SetNameRequestAsync(SetNameFunction setNameFunction)
        {
             return ContractHandler.SendRequestAsync(setNameFunction);
        }

        public Task<TransactionReceipt> SetNameRequestAndWaitForReceiptAsync(SetNameFunction setNameFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setNameFunction, token);
        }

        public Task<string> SetNameRequestAsync(byte[] node, string name)
        {
            var setNameFunction = new SetNameFunction();
                setNameFunction.Node = node;
                setNameFunction.Name = name;
            
             return ContractHandler.SendRequestAsync(setNameFunction);
        }

        public Task<TransactionReceipt> SetNameRequestAndWaitForReceiptAsync(byte[] node, string name, CancellationToken token = default(CancellationToken))
        {
            var setNameFunction = new SetNameFunction();
                setNameFunction.Node = node;
                setNameFunction.Name = name;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setNameFunction, token);
        }
    }
}
