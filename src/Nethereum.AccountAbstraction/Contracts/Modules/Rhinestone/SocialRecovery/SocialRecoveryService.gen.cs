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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery
{
    public partial class SocialRecoveryService: SocialRecoveryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryDeployment socialRecoveryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SocialRecoveryDeployment>().SendRequestAndWaitForReceiptAsync(socialRecoveryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryDeployment socialRecoveryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SocialRecoveryDeployment>().SendRequestAsync(socialRecoveryDeployment);
        }

        public static async Task<SocialRecoveryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SocialRecoveryDeployment socialRecoveryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, socialRecoveryDeployment, cancellationTokenSource);
            return new SocialRecoveryService(web3, receipt.ContractAddress);
        }

        public SocialRecoveryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SocialRecoveryServiceBase: ContractWeb3ServiceBase
    {

        public SocialRecoveryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AddGuardianRequestAsync(AddGuardianFunction addGuardianFunction)
        {
             return ContractHandler.SendRequestAsync(addGuardianFunction);
        }

        public virtual Task<TransactionReceipt> AddGuardianRequestAndWaitForReceiptAsync(AddGuardianFunction addGuardianFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addGuardianFunction, cancellationToken);
        }

        public virtual Task<string> AddGuardianRequestAsync(string guardian)
        {
            var addGuardianFunction = new AddGuardianFunction();
                addGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAsync(addGuardianFunction);
        }

        public virtual Task<TransactionReceipt> AddGuardianRequestAndWaitForReceiptAsync(string guardian, CancellationTokenSource cancellationToken = null)
        {
            var addGuardianFunction = new AddGuardianFunction();
                addGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addGuardianFunction, cancellationToken);
        }

        public Task<List<string>> GetGuardiansQueryAsync(GetGuardiansFunction getGuardiansFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetGuardiansFunction, List<string>>(getGuardiansFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetGuardiansQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getGuardiansFunction = new GetGuardiansFunction();
                getGuardiansFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetGuardiansFunction, List<string>>(getGuardiansFunction, blockParameter);
        }

        public Task<BigInteger> GuardianCountQueryAsync(GuardianCountFunction guardianCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GuardianCountFunction, BigInteger>(guardianCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GuardianCountQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var guardianCountFunction = new GuardianCountFunction();
                guardianCountFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<GuardianCountFunction, BigInteger>(guardianCountFunction, blockParameter);
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

        
        public virtual Task<byte[]> IsValidSignatureWithSenderQueryAsync(string returnValue1, byte[] returnValue2, byte[] returnValue3, BlockParameter blockParameter = null)
        {
            var isValidSignatureWithSenderFunction = new IsValidSignatureWithSenderFunction();
                isValidSignatureWithSenderFunction.ReturnValue1 = returnValue1;
                isValidSignatureWithSenderFunction.ReturnValue2 = returnValue2;
                isValidSignatureWithSenderFunction.ReturnValue3 = returnValue3;
            
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

        public virtual Task<string> RemoveGuardianRequestAsync(RemoveGuardianFunction removeGuardianFunction)
        {
             return ContractHandler.SendRequestAsync(removeGuardianFunction);
        }

        public virtual Task<TransactionReceipt> RemoveGuardianRequestAndWaitForReceiptAsync(RemoveGuardianFunction removeGuardianFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeGuardianFunction, cancellationToken);
        }

        public virtual Task<string> RemoveGuardianRequestAsync(string prevGuardian, string guardian)
        {
            var removeGuardianFunction = new RemoveGuardianFunction();
                removeGuardianFunction.PrevGuardian = prevGuardian;
                removeGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAsync(removeGuardianFunction);
        }

        public virtual Task<TransactionReceipt> RemoveGuardianRequestAndWaitForReceiptAsync(string prevGuardian, string guardian, CancellationTokenSource cancellationToken = null)
        {
            var removeGuardianFunction = new RemoveGuardianFunction();
                removeGuardianFunction.PrevGuardian = prevGuardian;
                removeGuardianFunction.Guardian = guardian;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeGuardianFunction, cancellationToken);
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
                typeof(AddGuardianFunction),
                typeof(GetGuardiansFunction),
                typeof(GuardianCountFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsValidSignatureWithSenderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(RemoveGuardianFunction),
                typeof(SetThresholdFunction),
                typeof(ThresholdFunction),
                typeof(ValidateUserOpFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(GuardianAddedEventDTO),
                typeof(GuardianRemovedEventDTO),
                typeof(ModuleInitializedEventDTO),
                typeof(ModuleUninitializedEventDTO),
                typeof(ThresholdSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(CannotRemoveGuardianError),
                typeof(InvalidGuardianError),
                typeof(InvalidSignatureError),
                typeof(InvalidThresholdError),
                typeof(LinkedlistAlreadyinitializedError),
                typeof(LinkedlistEntryalreadyinlistError),
                typeof(LinkedlistInvalidentryError),
                typeof(LinkedlistInvalidpageError),
                typeof(MaxGuardiansReachedError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(NotSortedAndUniqueError),
                typeof(ThresholdNotSetError),
                typeof(UnsupportedOperationError),
                typeof(WrongContractSignatureError),
                typeof(WrongContractSignatureFormatError)
            };
        }
    }
}
