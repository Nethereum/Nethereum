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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.MultiFactor.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.MultiFactor
{
    public partial class MultiFactorService: MultiFactorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, MultiFactorDeployment multiFactorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<MultiFactorDeployment>().SendRequestAndWaitForReceiptAsync(multiFactorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, MultiFactorDeployment multiFactorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<MultiFactorDeployment>().SendRequestAsync(multiFactorDeployment);
        }

        public static async Task<MultiFactorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, MultiFactorDeployment multiFactorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, multiFactorDeployment, cancellationTokenSource);
            return new MultiFactorService(web3, receipt.ContractAddress);
        }

        public MultiFactorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class MultiFactorServiceBase: ContractWeb3ServiceBase
    {

        public MultiFactorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> RegistryQueryAsync(RegistryFunction registryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryFunction, string>(registryFunction, blockParameter);
        }

        
        public virtual Task<string> RegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryFunction, string>(null, blockParameter);
        }

        public virtual Task<AccountConfigOutputDTO> AccountConfigQueryAsync(AccountConfigFunction accountConfigFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AccountConfigFunction, AccountConfigOutputDTO>(accountConfigFunction, blockParameter);
        }

        public virtual Task<AccountConfigOutputDTO> AccountConfigQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var accountConfigFunction = new AccountConfigFunction();
                accountConfigFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AccountConfigFunction, AccountConfigOutputDTO>(accountConfigFunction, blockParameter);
        }

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var isInitializedFunction = new IsInitializedFunction();
                isInitializedFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        public Task<bool> IsModuleTypeQueryAsync(IsModuleTypeFunction isModuleTypeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleTypeQueryAsync(BigInteger typeID, BlockParameter blockParameter = null)
        {
            var isModuleTypeFunction = new IsModuleTypeFunction();
                isModuleTypeFunction.TypeID = typeID;
            
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
        }

        public Task<bool> IsSubValidatorQueryAsync(IsSubValidatorFunction isSubValidatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsSubValidatorFunction, bool>(isSubValidatorFunction, blockParameter);
        }

        
        public virtual Task<bool> IsSubValidatorQueryAsync(string account, string subValidator, byte[] id, BlockParameter blockParameter = null)
        {
            var isSubValidatorFunction = new IsSubValidatorFunction();
                isSubValidatorFunction.Account = account;
                isSubValidatorFunction.SubValidator = subValidator;
                isSubValidatorFunction.Id = id;
            
            return ContractHandler.QueryAsync<IsSubValidatorFunction, bool>(isSubValidatorFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureWithSenderQueryAsync(IsValidSignatureWithSenderFunction isValidSignatureWithSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string returnValue1, byte[] hash, byte[] data, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.ReturnValue1 = returnValue1;
                isValidSignatureWithSenderFunction.Hash = hash;
                isValidSignatureWithSenderFunction.Data = data;
            
            return ContractHandler.QueryAsync<IsValidSignatureWithSenderFunction, byte[]>(isValidSignatureWithSenderFunction, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public virtual Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
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

        public virtual Task<string> RemoveValidatorRequestAsync(RemoveValidatorFunction removeValidatorFunction)
        {
             return ContractHandler.SendRequestAsync(removeValidatorFunction);
        }

        public virtual Task<TransactionReceipt> RemoveValidatorRequestAndWaitForReceiptAsync(RemoveValidatorFunction removeValidatorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeValidatorFunction, cancellationToken);
        }

        public virtual Task<string> RemoveValidatorRequestAsync(string validatorAddress, byte[] id)
        {
            var removeValidatorFunction = new RemoveValidatorFunction();
                removeValidatorFunction.ValidatorAddress = validatorAddress;
                removeValidatorFunction.Id = id;
            
             return ContractHandler.SendRequestAsync(removeValidatorFunction);
        }

        public virtual Task<TransactionReceipt> RemoveValidatorRequestAndWaitForReceiptAsync(string validatorAddress, byte[] id, CancellationTokenSource cancellationToken = null)
        {
            var removeValidatorFunction = new RemoveValidatorFunction();
                removeValidatorFunction.ValidatorAddress = validatorAddress;
                removeValidatorFunction.Id = id;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeValidatorFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(SetThresholdFunction setThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(SetThresholdFunction setThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(byte threshold)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(byte threshold, CancellationTokenSource cancellationToken = null)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public virtual Task<string> SetValidatorRequestAsync(SetValidatorFunction setValidatorFunction)
        {
             return ContractHandler.SendRequestAsync(setValidatorFunction);
        }

        public virtual Task<TransactionReceipt> SetValidatorRequestAndWaitForReceiptAsync(SetValidatorFunction setValidatorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setValidatorFunction, cancellationToken);
        }

        public virtual Task<string> SetValidatorRequestAsync(string validatorAddress, byte[] id, byte[] newValidatorData)
        {
            var setValidatorFunction = new SetValidatorFunction();
                setValidatorFunction.ValidatorAddress = validatorAddress;
                setValidatorFunction.Id = id;
                setValidatorFunction.NewValidatorData = newValidatorData;
            
             return ContractHandler.SendRequestAsync(setValidatorFunction);
        }

        public virtual Task<TransactionReceipt> SetValidatorRequestAndWaitForReceiptAsync(string validatorAddress, byte[] id, byte[] newValidatorData, CancellationTokenSource cancellationToken = null)
        {
            var setValidatorFunction = new SetValidatorFunction();
                setValidatorFunction.ValidatorAddress = validatorAddress;
                setValidatorFunction.Id = id;
                setValidatorFunction.NewValidatorData = newValidatorData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setValidatorFunction, cancellationToken);
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

        public Task<string> VersionQueryAsync(VersionFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(versionFunction, blockParameter);
        }

        
        public virtual Task<string> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(RegistryFunction),
                typeof(AccountConfigFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsSubValidatorFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(RemoveValidatorFunction),
                typeof(SetThresholdFunction),
                typeof(SetValidatorFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(IterationIncreasedEventDTO),
                typeof(ThesholdSetEventDTO),
                typeof(ValidatorAddedEventDTO),
                typeof(ValidatorRemovedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(InvalidThresholdError),
                typeof(InvalidValidatorDataError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(ZeroThresholdError)
            };
        }
    }
}
