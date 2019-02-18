using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.Deed.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class DeedService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, DeedDeployment deedDeployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<DeedDeployment>().SendRequestAndWaitForReceiptAsync(deedDeployment, token);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, DeedDeployment deedDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DeedDeployment>().SendRequestAsync(deedDeployment);
        }
        public static async Task<DeedService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, DeedDeployment deedDeployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, deedDeployment, token);
            return new DeedService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public DeedService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<BigInteger> CreationDateQueryAsync(CreationDateFunction creationDateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CreationDateFunction, BigInteger>(creationDateFunction, blockParameter);
        }

        
        public Task<BigInteger> CreationDateQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CreationDateFunction, BigInteger>(null, blockParameter);
        }



        public Task<string> DestroyDeedRequestAsync(DestroyDeedFunction destroyDeedFunction)
        {
             return ContractHandler.SendRequestAsync(destroyDeedFunction);
        }

        public Task<string> DestroyDeedRequestAsync()
        {
             return ContractHandler.SendRequestAsync<DestroyDeedFunction>();
        }

        public Task<TransactionReceipt> DestroyDeedRequestAndWaitForReceiptAsync(DestroyDeedFunction destroyDeedFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(destroyDeedFunction, token);
        }

        public Task<TransactionReceipt> DestroyDeedRequestAndWaitForReceiptAsync(CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<DestroyDeedFunction>(null, token);
        }

        public Task<string> SetOwnerRequestAsync(SetOwnerFunction setOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(SetOwnerFunction setOwnerFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, token);
        }

        public Task<string> SetOwnerRequestAsync(string newOwner)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(string newOwner, CancellationToken token = default(CancellationToken))
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, token);
        }

        public Task<string> RegistrarQueryAsync(RegistrarFunction registrarFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarFunction, string>(registrarFunction, blockParameter);
        }

        
        public Task<string> RegistrarQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarFunction, string>(null, blockParameter);
        }



        public Task<BigInteger> ValueQueryAsync(ValueFunction valueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValueFunction, BigInteger>(valueFunction, blockParameter);
        }

        
        public Task<BigInteger> ValueQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValueFunction, BigInteger>(null, blockParameter);
        }



        public Task<string> PreviousOwnerQueryAsync(PreviousOwnerFunction previousOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PreviousOwnerFunction, string>(previousOwnerFunction, blockParameter);
        }

        
        public Task<string> PreviousOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PreviousOwnerFunction, string>(null, blockParameter);
        }



        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }



        public Task<string> SetBalanceRequestAsync(SetBalanceFunction setBalanceFunction)
        {
             return ContractHandler.SendRequestAsync(setBalanceFunction);
        }

        public Task<TransactionReceipt> SetBalanceRequestAndWaitForReceiptAsync(SetBalanceFunction setBalanceFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setBalanceFunction, token);
        }

        public Task<string> SetBalanceRequestAsync(BigInteger newValue, bool throwOnFailure)
        {
            var setBalanceFunction = new SetBalanceFunction();
                setBalanceFunction.NewValue = newValue;
                setBalanceFunction.ThrowOnFailure = throwOnFailure;
            
             return ContractHandler.SendRequestAsync(setBalanceFunction);
        }

        public Task<TransactionReceipt> SetBalanceRequestAndWaitForReceiptAsync(BigInteger newValue, bool throwOnFailure, CancellationToken token = default(CancellationToken))
        {
            var setBalanceFunction = new SetBalanceFunction();
                setBalanceFunction.NewValue = newValue;
                setBalanceFunction.ThrowOnFailure = throwOnFailure;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setBalanceFunction, token);
        }

        public Task<string> CloseDeedRequestAsync(CloseDeedFunction closeDeedFunction)
        {
             return ContractHandler.SendRequestAsync(closeDeedFunction);
        }

        public Task<TransactionReceipt> CloseDeedRequestAndWaitForReceiptAsync(CloseDeedFunction closeDeedFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(closeDeedFunction, token);
        }

        public Task<string> CloseDeedRequestAsync(BigInteger refundRatio)
        {
            var closeDeedFunction = new CloseDeedFunction();
                closeDeedFunction.RefundRatio = refundRatio;
            
             return ContractHandler.SendRequestAsync(closeDeedFunction);
        }

        public Task<TransactionReceipt> CloseDeedRequestAndWaitForReceiptAsync(BigInteger refundRatio, CancellationToken token = default(CancellationToken))
        {
            var closeDeedFunction = new CloseDeedFunction();
                closeDeedFunction.RefundRatio = refundRatio;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(closeDeedFunction, token);
        }

        public Task<string> SetRegistrarRequestAsync(SetRegistrarFunction setRegistrarFunction)
        {
             return ContractHandler.SendRequestAsync(setRegistrarFunction);
        }

        public Task<TransactionReceipt> SetRegistrarRequestAndWaitForReceiptAsync(SetRegistrarFunction setRegistrarFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistrarFunction, token);
        }

        public Task<string> SetRegistrarRequestAsync(string newRegistrar)
        {
            var setRegistrarFunction = new SetRegistrarFunction();
                setRegistrarFunction.NewRegistrar = newRegistrar;
            
             return ContractHandler.SendRequestAsync(setRegistrarFunction);
        }

        public Task<TransactionReceipt> SetRegistrarRequestAndWaitForReceiptAsync(string newRegistrar, CancellationToken token = default(CancellationToken))
        {
            var setRegistrarFunction = new SetRegistrarFunction();
                setRegistrarFunction.NewRegistrar = newRegistrar;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistrarFunction, token);
        }
    }
}
