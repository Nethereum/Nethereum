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
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount
{
    public partial class NethereumAccountService: NethereumAccountServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountDeployment nethereumAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<NethereumAccountDeployment>().SendRequestAndWaitForReceiptAsync(nethereumAccountDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountDeployment nethereumAccountDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<NethereumAccountDeployment>().SendRequestAsync(nethereumAccountDeployment);
        }

        public static async Task<NethereumAccountService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountDeployment nethereumAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, nethereumAccountDeployment, cancellationTokenSource);
            return new NethereumAccountService(web3, receipt.ContractAddress);
        }

        public NethereumAccountService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class NethereumAccountServiceBase: ContractWeb3ServiceBase
    {

        public NethereumAccountServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> AccountIdQueryAsync(AccountIdFunction accountIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountIdFunction, string>(accountIdFunction, blockParameter);
        }

        
        public virtual Task<string> AccountIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountIdFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> EmergencyUninstallDelayQueryAsync(EmergencyUninstallDelayFunction emergencyUninstallDelayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EmergencyUninstallDelayFunction, BigInteger>(emergencyUninstallDelayFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> EmergencyUninstallDelayQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EmergencyUninstallDelayFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> UpgradeInterfaceVersionQueryAsync(UpgradeInterfaceVersionFunction upgradeInterfaceVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(upgradeInterfaceVersionFunction, blockParameter);
        }

        
        public virtual Task<string> UpgradeInterfaceVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(null, blockParameter);
        }

        public virtual Task<string> AddDepositRequestAsync(AddDepositFunction addDepositFunction)
        {
             return ContractHandler.SendRequestAsync(addDepositFunction);
        }

        public virtual Task<string> AddDepositRequestAsync()
        {
             return ContractHandler.SendRequestAsync<AddDepositFunction>();
        }

        public virtual Task<TransactionReceipt> AddDepositRequestAndWaitForReceiptAsync(AddDepositFunction addDepositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDepositFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> AddDepositRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<AddDepositFunction>(null, cancellationToken);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public virtual Task<string> ExecuteRequestAsync(ExecuteFunction executeFunction)
        {
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(ExecuteFunction executeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(byte[] mode, byte[] executionCalldata)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Mode = mode;
                executeFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(byte[] mode, byte[] executionCalldata, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Mode = mode;
                executeFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteFromExecutorRequestAsync(ExecuteFromExecutorFunction executeFromExecutorFunction)
        {
             return ContractHandler.SendRequestAsync(executeFromExecutorFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteFromExecutorRequestAndWaitForReceiptAsync(ExecuteFromExecutorFunction executeFromExecutorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFromExecutorFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteFromExecutorRequestAsync(byte[] mode, byte[] executionCalldata)
        {
            var executeFromExecutorFunction = new ExecuteFromExecutorFunction();
                executeFromExecutorFunction.Mode = mode;
                executeFromExecutorFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAsync(executeFromExecutorFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteFromExecutorRequestAndWaitForReceiptAsync(byte[] mode, byte[] executionCalldata, CancellationTokenSource cancellationToken = null)
        {
            var executeFromExecutorFunction = new ExecuteFromExecutorFunction();
                executeFromExecutorFunction.Mode = mode;
                executeFromExecutorFunction.ExecutionCalldata = executionCalldata;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFromExecutorFunction, cancellationToken);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<GetExecutorsPaginatedOutputDTO> GetExecutorsPaginatedQueryAsync(GetExecutorsPaginatedFunction getExecutorsPaginatedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetExecutorsPaginatedFunction, GetExecutorsPaginatedOutputDTO>(getExecutorsPaginatedFunction, blockParameter);
        }

        public virtual Task<GetExecutorsPaginatedOutputDTO> GetExecutorsPaginatedQueryAsync(string start, BigInteger pageSize, BlockParameter blockParameter = null)
        {
            var getExecutorsPaginatedFunction = new GetExecutorsPaginatedFunction();
                getExecutorsPaginatedFunction.Start = start;
                getExecutorsPaginatedFunction.PageSize = pageSize;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetExecutorsPaginatedFunction, GetExecutorsPaginatedOutputDTO>(getExecutorsPaginatedFunction, blockParameter);
        }

        public Task<BigInteger> GetNonceQueryAsync(GetNonceFunction getNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetNonceQueryAsync(BigInteger key, BlockParameter blockParameter = null)
        {
            var getNonceFunction = new GetNonceFunction();
                getNonceFunction.Key = key;
            
            return ContractHandler.QueryAsync<GetNonceFunction, BigInteger>(getNonceFunction, blockParameter);
        }

        public virtual Task<GetValidatorsPaginatedOutputDTO> GetValidatorsPaginatedQueryAsync(GetValidatorsPaginatedFunction getValidatorsPaginatedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetValidatorsPaginatedFunction, GetValidatorsPaginatedOutputDTO>(getValidatorsPaginatedFunction, blockParameter);
        }

        public virtual Task<GetValidatorsPaginatedOutputDTO> GetValidatorsPaginatedQueryAsync(string start, BigInteger pageSize, BlockParameter blockParameter = null)
        {
            var getValidatorsPaginatedFunction = new GetValidatorsPaginatedFunction();
                getValidatorsPaginatedFunction.Start = start;
                getValidatorsPaginatedFunction.PageSize = pageSize;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetValidatorsPaginatedFunction, GetValidatorsPaginatedOutputDTO>(getValidatorsPaginatedFunction, blockParameter);
        }

        public virtual Task<string> InitializeAccountRequestAsync(InitializeAccountFunction initializeAccountFunction)
        {
             return ContractHandler.SendRequestAsync(initializeAccountFunction);
        }

        public virtual Task<TransactionReceipt> InitializeAccountRequestAndWaitForReceiptAsync(InitializeAccountFunction initializeAccountFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeAccountFunction, cancellationToken);
        }

        public virtual Task<string> InitializeAccountRequestAsync(byte[] initData)
        {
            var initializeAccountFunction = new InitializeAccountFunction();
                initializeAccountFunction.InitData = initData;
            
             return ContractHandler.SendRequestAsync(initializeAccountFunction);
        }

        public virtual Task<TransactionReceipt> InitializeAccountRequestAndWaitForReceiptAsync(byte[] initData, CancellationTokenSource cancellationToken = null)
        {
            var initializeAccountFunction = new InitializeAccountFunction();
                initializeAccountFunction.InitData = initData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeAccountFunction, cancellationToken);
        }

        public virtual Task<string> InstallModuleRequestAsync(InstallModuleFunction installModuleFunction)
        {
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public virtual Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(InstallModuleFunction installModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public virtual Task<string> InstallModuleRequestAsync(BigInteger moduleTypeId, string module, byte[] initData)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.ModuleTypeId = moduleTypeId;
                installModuleFunction.Module = module;
                installModuleFunction.InitData = initData;
            
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public virtual Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(BigInteger moduleTypeId, string module, byte[] initData, CancellationTokenSource cancellationToken = null)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.ModuleTypeId = moduleTypeId;
                installModuleFunction.Module = module;
                installModuleFunction.InitData = initData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public Task<bool> IsModuleInstalledQueryAsync(IsModuleInstalledFunction isModuleInstalledFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsModuleInstalledFunction, bool>(isModuleInstalledFunction, blockParameter);
        }

        
        public virtual Task<bool> IsModuleInstalledQueryAsync(BigInteger moduleTypeId, string module, byte[] additionalContext, BlockParameter blockParameter = null)
        {
            var isModuleInstalledFunction = new IsModuleInstalledFunction();
                isModuleInstalledFunction.ModuleTypeId = moduleTypeId;
                isModuleInstalledFunction.Module = module;
                isModuleInstalledFunction.AdditionalContext = additionalContext;
            
            return ContractHandler.QueryAsync<IsModuleInstalledFunction, bool>(isModuleInstalledFunction, blockParameter);
        }

        public Task<byte[]> IsValidSignatureQueryAsync(IsValidSignatureFunction isValidSignatureFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSignatureFunction, byte[]>(isValidSignatureFunction, blockParameter);
        }

        
        public virtual Task<byte[]> IsValidSignatureQueryAsync(byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var isValidSignatureFunction = new IsValidSignatureFunction();
                isValidSignatureFunction.Hash = hash;
                isValidSignatureFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<IsValidSignatureFunction, byte[]>(isValidSignatureFunction, blockParameter);
        }

        public Task<byte[]> ProxiableUUIDQueryAsync(ProxiableUUIDFunction proxiableUUIDFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(proxiableUUIDFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ProxiableUUIDQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(null, blockParameter);
        }

        public Task<bool> SupportsExecutionModeQueryAsync(SupportsExecutionModeFunction supportsExecutionModeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsExecutionModeFunction, bool>(supportsExecutionModeFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsExecutionModeQueryAsync(byte[] encodedMode, BlockParameter blockParameter = null)
        {
            var supportsExecutionModeFunction = new SupportsExecutionModeFunction();
                supportsExecutionModeFunction.EncodedMode = encodedMode;
            
            return ContractHandler.QueryAsync<SupportsExecutionModeFunction, bool>(supportsExecutionModeFunction, blockParameter);
        }

        public Task<bool> SupportsModuleQueryAsync(SupportsModuleFunction supportsModuleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsModuleFunction, bool>(supportsModuleFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsModuleQueryAsync(BigInteger moduleTypeId, BlockParameter blockParameter = null)
        {
            var supportsModuleFunction = new SupportsModuleFunction();
                supportsModuleFunction.ModuleTypeId = moduleTypeId;
            
            return ContractHandler.QueryAsync<SupportsModuleFunction, bool>(supportsModuleFunction, blockParameter);
        }

        public virtual Task<string> UninstallModuleRequestAsync(UninstallModuleFunction uninstallModuleFunction)
        {
             return ContractHandler.SendRequestAsync(uninstallModuleFunction);
        }

        public virtual Task<TransactionReceipt> UninstallModuleRequestAndWaitForReceiptAsync(UninstallModuleFunction uninstallModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uninstallModuleFunction, cancellationToken);
        }

        public virtual Task<string> UninstallModuleRequestAsync(BigInteger moduleTypeId, string module, byte[] deInitData)
        {
            var uninstallModuleFunction = new UninstallModuleFunction();
                uninstallModuleFunction.ModuleTypeId = moduleTypeId;
                uninstallModuleFunction.Module = module;
                uninstallModuleFunction.DeInitData = deInitData;
            
             return ContractHandler.SendRequestAsync(uninstallModuleFunction);
        }

        public virtual Task<TransactionReceipt> UninstallModuleRequestAndWaitForReceiptAsync(BigInteger moduleTypeId, string module, byte[] deInitData, CancellationTokenSource cancellationToken = null)
        {
            var uninstallModuleFunction = new UninstallModuleFunction();
                uninstallModuleFunction.ModuleTypeId = moduleTypeId;
                uninstallModuleFunction.Module = module;
                uninstallModuleFunction.DeInitData = deInitData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uninstallModuleFunction, cancellationToken);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(UpgradeToAndCallFunction upgradeToAndCallFunction)
        {
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(UpgradeToAndCallFunction upgradeToAndCallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(string newImplementation, byte[] data)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(string newImplementation, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(ValidateUserOpFunction validateUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(ValidateUserOpFunction validateUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger missingAccountFunds)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
                validateUserOpFunction.MissingAccountFunds = missingAccountFunds;
            
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger missingAccountFunds, CancellationTokenSource cancellationToken = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
                validateUserOpFunction.MissingAccountFunds = missingAccountFunds;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawDepositToRequestAsync(WithdrawDepositToFunction withdrawDepositToFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawDepositToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawDepositToRequestAndWaitForReceiptAsync(WithdrawDepositToFunction withdrawDepositToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawDepositToFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawDepositToRequestAsync(string to, BigInteger amount)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.To = to;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawDepositToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawDepositToRequestAndWaitForReceiptAsync(string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.To = to;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawDepositToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountIdFunction),
                typeof(EmergencyUninstallDelayFunction),
                typeof(UpgradeInterfaceVersionFunction),
                typeof(AccountIdFunction),
                typeof(AddDepositFunction),
                typeof(EntryPointFunction),
                typeof(ExecuteFunction),
                typeof(ExecuteFromExecutorFunction),
                typeof(GetDepositFunction),
                typeof(GetExecutorsPaginatedFunction),
                typeof(GetNonceFunction),
                typeof(GetValidatorsPaginatedFunction),
                typeof(InitializeAccountFunction),
                typeof(InstallModuleFunction),
                typeof(IsModuleInstalledFunction),
                typeof(IsValidSignatureFunction),
                typeof(ProxiableUUIDFunction),
                typeof(SupportsExecutionModeFunction),
                typeof(SupportsModuleFunction),
                typeof(UninstallModuleFunction),
                typeof(UpgradeToAndCallFunction),
                typeof(ValidateUserOpFunction),
                typeof(WithdrawDepositToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountInitializedEventDTO),
                typeof(DelegatecallExecutedEventDTO),
                typeof(InitializedEventDTO),
                typeof(ModuleInstalledEventDTO),
                typeof(ModuleUninstalledEventDTO),
                typeof(TryExecutionFailedEventDTO),
                typeof(UpgradedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AddressEmptyCodeError),
                typeof(CannotRemoveLastValidatorError),
                typeof(DelegatecallFailedError),
                typeof(ERC1967InvalidImplementationError),
                typeof(ERC1967NonPayableError),
                typeof(EmergencyTimelockActiveError),
                typeof(EmergencyTimelockNotReadyError),
                typeof(ExecutionFailedError),
                typeof(FailedCallError),
                typeof(FallbackHandlerNotInstalledError),
                typeof(FallbackSelectorAlreadyInstalledError),
                typeof(FallbackSelectorNotInstalledError),
                typeof(ForbiddenSelectorError),
                typeof(HookAlreadyInstalledError),
                typeof(InvalidEntryPointError),
                typeof(InvalidInitializationError),
                typeof(InvalidModuleError),
                typeof(InvalidModuleTypeError),
                typeof(InvalidSignatureLengthError),
                typeof(LinkedlistAlreadyinitializedError),
                typeof(LinkedlistEntryalreadyinlistError),
                typeof(LinkedlistInvalidentryError),
                typeof(LinkedlistInvalidpreventryError),
                typeof(ModuleAlreadyInstalledError),
                typeof(ModuleNotInstalledError),
                typeof(NotInitializingError),
                typeof(OnlyEntryPointError),
                typeof(OnlyEntryPointOrSelfError),
                typeof(OnlyExecutorModuleError),
                typeof(UUPSUnauthorizedCallContextError),
                typeof(UUPSUnsupportedProxiableUUIDError),
                typeof(UnsupportedCallTypeError),
                typeof(UnsupportedExecTypeError)
            };
        }
    }
}
