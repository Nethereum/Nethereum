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
using Nethereum.AccountAbstraction.Contracts.Core.ModularSmartAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.ModularSmartAccount
{
    public partial class ModularSmartAccountService: ModularSmartAccountServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ModularSmartAccountDeployment modularSmartAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ModularSmartAccountDeployment>().SendRequestAndWaitForReceiptAsync(modularSmartAccountDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ModularSmartAccountDeployment modularSmartAccountDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ModularSmartAccountDeployment>().SendRequestAsync(modularSmartAccountDeployment);
        }

        public static async Task<ModularSmartAccountService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ModularSmartAccountDeployment modularSmartAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, modularSmartAccountDeployment, cancellationTokenSource);
            return new ModularSmartAccountService(web3, receipt.ContractAddress);
        }

        public ModularSmartAccountService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ModularSmartAccountServiceBase: ContractWeb3ServiceBase
    {

        public ModularSmartAccountServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> UpgradeInterfaceVersionQueryAsync(UpgradeInterfaceVersionFunction upgradeInterfaceVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(upgradeInterfaceVersionFunction, blockParameter);
        }

        
        public virtual Task<string> UpgradeInterfaceVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(null, blockParameter);
        }

        public Task<string> AccountRegistryQueryAsync(AccountRegistryFunction accountRegistryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(accountRegistryFunction, blockParameter);
        }

        
        public virtual Task<string> AccountRegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(null, blockParameter);
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

        public virtual Task<string> ChangeOwnerRequestAsync(ChangeOwnerFunction changeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(changeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerRequestAndWaitForReceiptAsync(ChangeOwnerFunction changeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerFunction, cancellationToken);
        }

        public virtual Task<string> ChangeOwnerRequestAsync(string newOwner)
        {
            var changeOwnerFunction = new ChangeOwnerFunction();
                changeOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(changeOwnerFunction);
        }

        public virtual Task<TransactionReceipt> ChangeOwnerRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var changeOwnerFunction = new ChangeOwnerFunction();
                changeOwnerFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeOwnerFunction, cancellationToken);
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

        public virtual Task<string> ExecuteRequestAsync(string target, BigInteger value, byte[] data)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Target = target;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(string target, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Target = target;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(ExecuteBatchFunction executeBatchFunction)
        {
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(ExecuteBatchFunction executeBatchFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(List<string> targets, List<BigInteger> values, List<byte[]> datas)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Targets = targets;
                executeBatchFunction.Values = values;
                executeBatchFunction.Datas = datas;
            
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(List<string> targets, List<BigInteger> values, List<byte[]> datas, CancellationTokenSource cancellationToken = null)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Targets = targets;
                executeBatchFunction.Values = values;
                executeBatchFunction.Datas = datas;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
        }

        public Task<List<byte[]>> GetInstalledModulesQueryAsync(GetInstalledModulesFunction getInstalledModulesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetInstalledModulesFunction, List<byte[]>>(getInstalledModulesFunction, blockParameter);
        }

        
        public virtual Task<List<byte[]>> GetInstalledModulesQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetInstalledModulesFunction, List<byte[]>>(null, blockParameter);
        }

        public Task<string> GetModuleQueryAsync(GetModuleFunction getModuleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetModuleFunction, string>(getModuleFunction, blockParameter);
        }

        
        public virtual Task<string> GetModuleQueryAsync(byte[] moduleId, BlockParameter blockParameter = null)
        {
            var getModuleFunction = new GetModuleFunction();
                getModuleFunction.ModuleId = moduleId;
            
            return ContractHandler.QueryAsync<GetModuleFunction, string>(getModuleFunction, blockParameter);
        }

        public Task<BigInteger> GetModuleCountQueryAsync(GetModuleCountFunction getModuleCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetModuleCountFunction, BigInteger>(getModuleCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetModuleCountQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetModuleCountFunction, BigInteger>(null, blockParameter);
        }

        public Task<bool> HasModuleQueryAsync(HasModuleFunction hasModuleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HasModuleFunction, bool>(hasModuleFunction, blockParameter);
        }

        
        public virtual Task<bool> HasModuleQueryAsync(byte[] moduleId, BlockParameter blockParameter = null)
        {
            var hasModuleFunction = new HasModuleFunction();
                hasModuleFunction.ModuleId = moduleId;
            
            return ContractHandler.QueryAsync<HasModuleFunction, bool>(hasModuleFunction, blockParameter);
        }

        public virtual Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public virtual Task<string> InitializeRequestAsync(string owner, List<byte[]> moduleIds, List<string> moduleAddresses, List<bool> canValidate)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Owner = owner;
                initializeFunction.ModuleIds = moduleIds;
                initializeFunction.ModuleAddresses = moduleAddresses;
                initializeFunction.CanValidate = canValidate;
            
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(string owner, List<byte[]> moduleIds, List<string> moduleAddresses, List<bool> canValidate, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Owner = owner;
                initializeFunction.ModuleIds = moduleIds;
                initializeFunction.ModuleAddresses = moduleAddresses;
                initializeFunction.CanValidate = canValidate;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<byte[]> InstalledModuleIdsQueryAsync(InstalledModuleIdsFunction installedModuleIdsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InstalledModuleIdsFunction, byte[]>(installedModuleIdsFunction, blockParameter);
        }

        
        public virtual Task<byte[]> InstalledModuleIdsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var installedModuleIdsFunction = new InstalledModuleIdsFunction();
                installedModuleIdsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<InstalledModuleIdsFunction, byte[]>(installedModuleIdsFunction, blockParameter);
        }

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(null, blockParameter);
        }

        public Task<bool> ModuleCanValidateQueryAsync(ModuleCanValidateFunction moduleCanValidateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleCanValidateFunction, bool>(moduleCanValidateFunction, blockParameter);
        }

        
        public virtual Task<bool> ModuleCanValidateQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var moduleCanValidateFunction = new ModuleCanValidateFunction();
                moduleCanValidateFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ModuleCanValidateFunction, bool>(moduleCanValidateFunction, blockParameter);
        }

        public Task<string> ModulesQueryAsync(ModulesFunction modulesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModulesFunction, string>(modulesFunction, blockParameter);
        }

        
        public virtual Task<string> ModulesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var modulesFunction = new ModulesFunction();
                modulesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ModulesFunction, string>(modulesFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<byte[]> ProxiableUUIDQueryAsync(ProxiableUUIDFunction proxiableUUIDFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(proxiableUUIDFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ProxiableUUIDQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(null, blockParameter);
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

        public virtual Task<string> WithdrawDepositToRequestAsync(string withdrawAddress, BigInteger amount)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.WithdrawAddress = withdrawAddress;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawDepositToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawDepositToRequestAndWaitForReceiptAsync(string withdrawAddress, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.WithdrawAddress = withdrawAddress;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawDepositToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(UpgradeInterfaceVersionFunction),
                typeof(AccountRegistryFunction),
                typeof(AddDepositFunction),
                typeof(ChangeOwnerFunction),
                typeof(EntryPointFunction),
                typeof(ExecuteFunction),
                typeof(ExecuteBatchFunction),
                typeof(GetDepositFunction),
                typeof(GetInstalledModulesFunction),
                typeof(GetModuleFunction),
                typeof(GetModuleCountFunction),
                typeof(HasModuleFunction),
                typeof(InitializeFunction),
                typeof(InstalledModuleIdsFunction),
                typeof(IsInitializedFunction),
                typeof(ModuleCanValidateFunction),
                typeof(ModulesFunction),
                typeof(OwnerFunction),
                typeof(ProxiableUUIDFunction),
                typeof(UpgradeToAndCallFunction),
                typeof(ValidateUserOpFunction),
                typeof(WithdrawDepositToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(InitializedEventDTO),
                typeof(ModulesInstalledEventDTO),
                typeof(OwnerChangedEventDTO),
                typeof(UpgradedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AccountNotActiveError),
                typeof(AddressEmptyCodeError),
                typeof(AlreadyInitializedError),
                typeof(ArrayLengthMismatchError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(ERC1967InvalidImplementationError),
                typeof(ERC1967NonPayableError),
                typeof(ExecutionFailedError),
                typeof(FailedCallError),
                typeof(InvalidInitializationError),
                typeof(InvalidModuleIdError),
                typeof(ModuleNotInstalledError),
                typeof(NotInitializingError),
                typeof(OnlyEntryPointError),
                typeof(OnlyOwnerError),
                typeof(OnlyOwnerOrEntryPointError),
                typeof(UUPSUnauthorizedCallContextError),
                typeof(UUPSUnsupportedProxiableUUIDError)
            };
        }
    }
}
