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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator
{
    public partial class OwnableValidatorService: OwnableValidatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, OwnableValidatorDeployment ownableValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<OwnableValidatorDeployment>().SendRequestAndWaitForReceiptAsync(ownableValidatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, OwnableValidatorDeployment ownableValidatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<OwnableValidatorDeployment>().SendRequestAsync(ownableValidatorDeployment);
        }

        public static async Task<OwnableValidatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, OwnableValidatorDeployment ownableValidatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ownableValidatorDeployment, cancellationTokenSource);
            return new OwnableValidatorService(web3, receipt.ContractAddress);
        }

        public OwnableValidatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class OwnableValidatorServiceBase: ContractWeb3ServiceBase
    {

        public OwnableValidatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddOwnerRequestAsync(AddOwnerFunction addOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(addOwnerFunction);
        }

        public virtual Task<TransactionReceipt> AddOwnerRequestAndWaitForReceiptAsync(AddOwnerFunction addOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addOwnerFunction, cancellationToken);
        }

        public virtual Task<string> AddOwnerRequestAsync(string owner)
        {
            var addOwnerFunction = new AddOwnerFunction();
                addOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(addOwnerFunction);
        }

        public virtual Task<TransactionReceipt> AddOwnerRequestAndWaitForReceiptAsync(string owner, CancellationTokenSource cancellationToken = null)
        {
            var addOwnerFunction = new AddOwnerFunction();
                addOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addOwnerFunction, cancellationToken);
        }

        public Task<List<string>> GetOwnersQueryAsync(GetOwnersFunction getOwnersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetOwnersFunction, List<string>>(getOwnersFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetOwnersQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getOwnersFunction = new GetOwnersFunction();
                getOwnersFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetOwnersFunction, List<string>>(getOwnersFunction, blockParameter);
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

        
        public virtual Task<bool> IsModuleTypeQueryAsync(BigInteger typeID, BlockParameter blockParameter = null)
        {
            var isModuleTypeFunction = new IsModuleTypeFunction();
                isModuleTypeFunction.TypeID = typeID;
            
            return ContractHandler.QueryAsync<IsModuleTypeFunction, bool>(isModuleTypeFunction, blockParameter);
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

        public Task<BigInteger> OwnerCountQueryAsync(OwnerCountFunction ownerCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerCountFunction, BigInteger>(ownerCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> OwnerCountQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var ownerCountFunction = new OwnerCountFunction();
                ownerCountFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<OwnerCountFunction, BigInteger>(ownerCountFunction, blockParameter);
        }

        public virtual Task<string> RemoveOwnerRequestAsync(RemoveOwnerFunction removeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(removeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> RemoveOwnerRequestAndWaitForReceiptAsync(RemoveOwnerFunction removeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeOwnerFunction, cancellationToken);
        }

        public virtual Task<string> RemoveOwnerRequestAsync(string prevOwner, string owner)
        {
            var removeOwnerFunction = new RemoveOwnerFunction();
                removeOwnerFunction.PrevOwner = prevOwner;
                removeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(removeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> RemoveOwnerRequestAndWaitForReceiptAsync(string prevOwner, string owner, CancellationTokenSource cancellationToken = null)
        {
            var removeOwnerFunction = new RemoveOwnerFunction();
                removeOwnerFunction.PrevOwner = prevOwner;
                removeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeOwnerFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(SetThresholdFunction setThresholdFunction)
        {
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(SetThresholdFunction setThresholdFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public virtual Task<string> SetThresholdRequestAsync(BigInteger threshold)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAsync(setThresholdFunction);
        }

        public virtual Task<TransactionReceipt> SetThresholdRequestAndWaitForReceiptAsync(BigInteger threshold, CancellationTokenSource cancellationToken = null)
        {
            var setThresholdFunction = new SetThresholdFunction();
                setThresholdFunction.Threshold = threshold;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setThresholdFunction, cancellationToken);
        }

        public Task<BigInteger> ThresholdQueryAsync(ThresholdFunction thresholdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(thresholdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ThresholdQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var thresholdFunction = new ThresholdFunction();
                thresholdFunction.Account = account;
            
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(thresholdFunction, blockParameter);
        }

        public Task<bool> ValidateSignatureWithDataQueryAsync(ValidateSignatureWithDataFunction validateSignatureWithDataFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
        }

        
        public virtual Task<bool> ValidateSignatureWithDataQueryAsync(byte[] hash, byte[] signature, byte[] data, BlockParameter blockParameter = null)
        {
            var validateSignatureWithDataFunction = new ValidateSignatureWithDataFunction();
                validateSignatureWithDataFunction.Hash = hash;
                validateSignatureWithDataFunction.Signature = signature;
                validateSignatureWithDataFunction.Data = data;
            
            return ContractHandler.QueryAsync<ValidateSignatureWithDataFunction, bool>(validateSignatureWithDataFunction, blockParameter);
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
                typeof(AddOwnerFunction),
                typeof(GetOwnersFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(OwnerCountFunction),
                typeof(RemoveOwnerFunction),
                typeof(SetThresholdFunction),
                typeof(ThresholdFunction),
                typeof(ValidateSignatureWithDataFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ModuleInitializedEventDTO),
                typeof(ModuleUninitializedEventDTO),
                typeof(OwnerAddedEventDTO),
                typeof(OwnerRemovedEventDTO),
                typeof(ThresholdSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(CannotRemoveOwnerError),
                typeof(InvalidOwnerError),
                typeof(InvalidSignatureError),
                typeof(InvalidThresholdError),
                typeof(LinkedlistAlreadyinitializedError),
                typeof(LinkedlistEntryalreadyinlistError),
                typeof(LinkedlistInvalidentryError),
                typeof(LinkedlistInvalidpageError),
                typeof(MaxOwnersReachedError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(NotSortedAndUniqueError),
                typeof(ThresholdNotSetError),
                typeof(WrongContractSignatureError),
                typeof(WrongContractSignatureFormatError)
            };
        }
    }
}
