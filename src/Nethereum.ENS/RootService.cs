using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.ENS.Root.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class RootService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, RootDeployment rootDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<RootDeployment>().SendRequestAndWaitForReceiptAsync(rootDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, RootDeployment rootDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RootDeployment>().SendRequestAsync(rootDeployment);
        }

        public static async Task<RootService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, RootDeployment rootDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, rootDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new RootService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public RootService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<bool> ControllersQueryAsync(ControllersFunction controllersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ControllersFunction, bool>(controllersFunction, blockParameter);
        }

        
        public Task<bool> ControllersQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var controllersFunction = new ControllersFunction();
                controllersFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ControllersFunction, bool>(controllersFunction, blockParameter);
        }

        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }

        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }

        public Task<bool> IsOwnerQueryAsync(IsOwnerFunction isOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameter);
        }

        
        public Task<bool> IsOwnerQueryAsync(string addr, BlockParameter blockParameter = null)
        {
            var isOwnerFunction = new IsOwnerFunction();
                isOwnerFunction.Addr = addr;
            
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameter);
        }

        public Task<string> LockRequestAsync(LockFunction lockFunction)
        {
             return ContractHandler.SendRequestAsync(lockFunction);
        }

        public Task<TransactionReceipt> LockRequestAndWaitForReceiptAsync(LockFunction lockFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(lockFunction, cancellationToken);
        }

        public Task<string> LockRequestAsync(byte[] label)
        {
            var lockFunction = new LockFunction();
                lockFunction.Label = label;
            
             return ContractHandler.SendRequestAsync(lockFunction);
        }

        public Task<TransactionReceipt> LockRequestAndWaitForReceiptAsync(byte[] label, CancellationTokenSource cancellationToken = null)
        {
            var lockFunction = new LockFunction();
                lockFunction.Label = label;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(lockFunction, cancellationToken);
        }

        public Task<bool> LockedQueryAsync(LockedFunction lockedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LockedFunction, bool>(lockedFunction, blockParameter);
        }

        
        public Task<bool> LockedQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var lockedFunction = new LockedFunction();
                lockedFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<LockedFunction, bool>(lockedFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> SetControllerRequestAsync(SetControllerFunction setControllerFunction)
        {
             return ContractHandler.SendRequestAsync(setControllerFunction);
        }

        public Task<TransactionReceipt> SetControllerRequestAndWaitForReceiptAsync(SetControllerFunction setControllerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setControllerFunction, cancellationToken);
        }

        public Task<string> SetControllerRequestAsync(string controller, bool enabled)
        {
            var setControllerFunction = new SetControllerFunction();
                setControllerFunction.Controller = controller;
                setControllerFunction.Enabled = enabled;
            
             return ContractHandler.SendRequestAsync(setControllerFunction);
        }

        public Task<TransactionReceipt> SetControllerRequestAndWaitForReceiptAsync(string controller, bool enabled, CancellationTokenSource cancellationToken = null)
        {
            var setControllerFunction = new SetControllerFunction();
                setControllerFunction.Controller = controller;
                setControllerFunction.Enabled = enabled;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setControllerFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(SetResolverFunction setResolverFunction)
        {
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(string resolver)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(string resolver, CancellationTokenSource cancellationToken = null)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<string> SetSubnodeOwnerRequestAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, cancellationToken);
        }

        public Task<string> SetSubnodeOwnerRequestAsync(byte[] label, string owner)
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(byte[] label, string owner, CancellationTokenSource cancellationToken = null)
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }
    }
}
