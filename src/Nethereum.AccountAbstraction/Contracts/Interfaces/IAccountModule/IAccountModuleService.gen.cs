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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountModule
{
    public partial class IAccountModuleService: IAccountModuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IAccountModuleDeployment iAccountModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountModuleDeployment>().SendRequestAndWaitForReceiptAsync(iAccountModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IAccountModuleDeployment iAccountModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountModuleDeployment>().SendRequestAsync(iAccountModuleDeployment);
        }

        public static async Task<IAccountModuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IAccountModuleDeployment iAccountModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iAccountModuleDeployment, cancellationTokenSource);
            return new IAccountModuleService(web3, receipt.ContractAddress);
        }

        public IAccountModuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IAccountModuleServiceBase: ContractWeb3ServiceBase
    {

        public IAccountModuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> ModuleIdQueryAsync(ModuleIdFunction moduleIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleIdFunction, byte[]>(moduleIdFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ModuleIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleIdFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<string> PostExecuteRequestAsync(PostExecuteFunction postExecuteFunction)
        {
             return ContractHandler.SendRequestAsync(postExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PostExecuteRequestAndWaitForReceiptAsync(PostExecuteFunction postExecuteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PostExecuteRequestAsync(string target, BigInteger value, byte[] data)
        {
            var postExecuteFunction = new PostExecuteFunction();
                postExecuteFunction.Target = target;
                postExecuteFunction.Value = value;
                postExecuteFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(postExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PostExecuteRequestAndWaitForReceiptAsync(string target, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var postExecuteFunction = new PostExecuteFunction();
                postExecuteFunction.Target = target;
                postExecuteFunction.Value = value;
                postExecuteFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PreExecuteRequestAsync(PreExecuteFunction preExecuteFunction)
        {
             return ContractHandler.SendRequestAsync(preExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PreExecuteRequestAndWaitForReceiptAsync(PreExecuteFunction preExecuteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preExecuteFunction, cancellationToken);
        }

        public virtual Task<string> PreExecuteRequestAsync(string target, BigInteger value, byte[] data)
        {
            var preExecuteFunction = new PreExecuteFunction();
                preExecuteFunction.Target = target;
                preExecuteFunction.Value = value;
                preExecuteFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(preExecuteFunction);
        }

        public virtual Task<TransactionReceipt> PreExecuteRequestAndWaitForReceiptAsync(string target, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var preExecuteFunction = new PreExecuteFunction();
                preExecuteFunction.Target = target;
                preExecuteFunction.Value = value;
                preExecuteFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preExecuteFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(ValidateUserOpFunction validateUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(ValidateUserOpFunction validateUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, CancellationTokenSource cancellationToken = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public Task<BigInteger> VersionQueryAsync(VersionFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, BigInteger>(versionFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, BigInteger>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ModuleIdFunction),
                typeof(PostExecuteFunction),
                typeof(PreExecuteFunction),
                typeof(SupportsInterfaceFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
