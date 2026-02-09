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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IValidator
{
    public partial class IValidatorService: IValidatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IValidatorDeployment iValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IValidatorDeployment>().SendRequestAndWaitForReceiptAsync(iValidatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IValidatorDeployment iValidatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IValidatorDeployment>().SendRequestAsync(iValidatorDeployment);
        }

        public static async Task<IValidatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IValidatorDeployment iValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iValidatorDeployment, cancellationTokenSource);
            return new IValidatorService(web3, receipt.ContractAddress);
        }

        public IValidatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IValidatorServiceBase: ContractWeb3ServiceBase
    {

        public IValidatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(string smartAccount, BlockParameter blockParameter = null)
        {
            var isInitializedFunction = new IsInitializedFunction();
                isInitializedFunction.SmartAccount = smartAccount;
            
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        public Task<bool> IsModuleTypeQueryAsync(IsModuleTypeFunction isModuleTypeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleTypeQueryAsync(BigInteger moduleTypeId, BlockParameter blockParameter = null)
        {
            var isModuleTypeFunction = new IsModuleTypeFunction();
                isModuleTypeFunction.ModuleTypeId = moduleTypeId;
            
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureWithSenderQueryAsync(IsValidSignatureWithSenderFunction isValidSignatureWithSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string sender, byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.Sender = sender;
                isValidSignatureWithSenderFunction.Hash = hash;
                isValidSignatureWithSenderFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        public virtual Task<string> OnInstallRequestAsync(OnInstallFunction onInstallFunction)
        {
             return ContractHandler.SendRequestAsync(onInstallFunction);
        }

        public virtual Task<TransactionReceipt> OnInstallRequestAndWaitForReceiptAsync(OnInstallFunction onInstallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onInstallFunction, cancellationToken);
        }

        public virtual Task<string> OnInstallRequestAsync(byte[] data)
        {
            var onInstallFunction = new OnInstallFunction();
                onInstallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(onInstallFunction);
        }

        public virtual Task<TransactionReceipt> OnInstallRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var onInstallFunction = new OnInstallFunction();
                onInstallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onInstallFunction, cancellationToken);
        }

        public virtual Task<string> OnUninstallRequestAsync(OnUninstallFunction onUninstallFunction)
        {
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(OnUninstallFunction onUninstallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onUninstallFunction, cancellationToken);
        }

        public virtual Task<string> OnUninstallRequestAsync(byte[] data)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onUninstallFunction, cancellationToken);
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(ValidateUserOpFunction)
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
