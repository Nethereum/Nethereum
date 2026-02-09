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
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator
{
    public partial class ECDSAValidatorService: ECDSAValidatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ECDSAValidatorDeployment eCDSAValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ECDSAValidatorDeployment>().SendRequestAndWaitForReceiptAsync(eCDSAValidatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ECDSAValidatorDeployment eCDSAValidatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ECDSAValidatorDeployment>().SendRequestAsync(eCDSAValidatorDeployment);
        }

        public static async Task<ECDSAValidatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ECDSAValidatorDeployment eCDSAValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eCDSAValidatorDeployment, cancellationTokenSource);
            return new ECDSAValidatorService(web3, receipt.ContractAddress);
        }

        public ECDSAValidatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ECDSAValidatorServiceBase: ContractWeb3ServiceBase
    {

        public ECDSAValidatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> GetOwnerQueryAsync(GetOwnerFunction getOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetOwnerFunction, string>(getOwnerFunction, blockParameter);
        }

        
        public virtual Task<string> GetOwnerQueryAsync(string smartAccount, BlockParameter blockParameter = null)
        {
            var getOwnerFunction = new GetOwnerFunction();
                getOwnerFunction.SmartAccount = smartAccount;
            
            return ContractHandler.QueryAsync<GetOwnerFunction, string>(getOwnerFunction, blockParameter);
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

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string returnValue1, byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.ReturnValue1 = returnValue1;
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

        public virtual Task<string> OnUninstallRequestAsync(byte[] returnValue1)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.ReturnValue1 = returnValue1;
            
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(byte[] returnValue1, CancellationTokenSource cancellationToken = null)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.ReturnValue1 = returnValue1;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onUninstallFunction, cancellationToken);
        }

        public Task<string> OwnersQueryAsync(OwnersFunction ownersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnersFunction, string>(ownersFunction, blockParameter);
        }

        
        public virtual Task<string> OwnersQueryAsync(string smartAccount, BlockParameter blockParameter = null)
        {
            var ownersFunction = new OwnersFunction();
                ownersFunction.SmartAccount = smartAccount;
            
            return ContractHandler.QueryAsync<OwnersFunction, string>(ownersFunction, blockParameter);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<BigInteger> ValidateUserOpQueryAsync(ValidateUserOpFunction validateUserOpFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ValidateUserOpQueryAsync(PackedUserOperation userOp, byte[] userOpHash, BlockParameter blockParameter = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
            
            return ContractHandler.QueryAsync<ValidateUserOpFunction, BigInteger>(validateUserOpFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(GetOwnerFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(OwnersFunction),
                typeof(TransferOwnershipFunction),
                typeof(ValidateUserOpFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(OwnerSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AlreadyInitializedError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(InvalidOwnerError),
                typeof(NotInitializedError)
            };
        }
    }
}
