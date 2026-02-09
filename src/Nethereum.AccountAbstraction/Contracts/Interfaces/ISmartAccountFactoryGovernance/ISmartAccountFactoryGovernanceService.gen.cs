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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ISmartAccountFactoryGovernance.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ISmartAccountFactoryGovernance
{
    public partial class ISmartAccountFactoryGovernanceService: ISmartAccountFactoryGovernanceServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ISmartAccountFactoryGovernanceDeployment iSmartAccountFactoryGovernanceDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ISmartAccountFactoryGovernanceDeployment>().SendRequestAndWaitForReceiptAsync(iSmartAccountFactoryGovernanceDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ISmartAccountFactoryGovernanceDeployment iSmartAccountFactoryGovernanceDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ISmartAccountFactoryGovernanceDeployment>().SendRequestAsync(iSmartAccountFactoryGovernanceDeployment);
        }

        public static async Task<ISmartAccountFactoryGovernanceService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ISmartAccountFactoryGovernanceDeployment iSmartAccountFactoryGovernanceDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iSmartAccountFactoryGovernanceDeployment, cancellationTokenSource);
            return new ISmartAccountFactoryGovernanceService(web3, receipt.ContractAddress);
        }

        public ISmartAccountFactoryGovernanceService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ISmartAccountFactoryGovernanceServiceBase: ContractWeb3ServiceBase
    {

        public ISmartAccountFactoryGovernanceServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> MaxAdminsQueryAsync(MaxAdminsFunction maxAdminsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxAdminsFunction, BigInteger>(maxAdminsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxAdminsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxAdminsFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> AccountRegistryQueryAsync(AccountRegistryFunction accountRegistryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(accountRegistryFunction, blockParameter);
        }

        
        public virtual Task<string> AccountRegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(null, blockParameter);
        }

        public virtual Task<string> CreateAccountRequestAsync(CreateAccountFunction createAccountFunction)
        {
             return ContractHandler.SendRequestAsync(createAccountFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountRequestAndWaitForReceiptAsync(CreateAccountFunction createAccountFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountFunction, cancellationToken);
        }

        public virtual Task<string> CreateAccountRequestAsync(string owner, byte[] salt, List<byte[]> moduleIds)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Owner = owner;
                createAccountFunction.Salt = salt;
                createAccountFunction.ModuleIds = moduleIds;
            
             return ContractHandler.SendRequestAsync(createAccountFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountRequestAndWaitForReceiptAsync(string owner, byte[] salt, List<byte[]> moduleIds, CancellationTokenSource cancellationToken = null)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Owner = owner;
                createAccountFunction.Salt = salt;
                createAccountFunction.ModuleIds = moduleIds;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountFunction, cancellationToken);
        }

        public virtual Task<string> CreateAccountIfNeededRequestAsync(CreateAccountIfNeededFunction createAccountIfNeededFunction)
        {
             return ContractHandler.SendRequestAsync(createAccountIfNeededFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountIfNeededRequestAndWaitForReceiptAsync(CreateAccountIfNeededFunction createAccountIfNeededFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountIfNeededFunction, cancellationToken);
        }

        public virtual Task<string> CreateAccountIfNeededRequestAsync(string owner, byte[] salt, List<byte[]> moduleIds)
        {
            var createAccountIfNeededFunction = new CreateAccountIfNeededFunction();
                createAccountIfNeededFunction.Owner = owner;
                createAccountIfNeededFunction.Salt = salt;
                createAccountIfNeededFunction.ModuleIds = moduleIds;
            
             return ContractHandler.SendRequestAsync(createAccountIfNeededFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountIfNeededRequestAndWaitForReceiptAsync(string owner, byte[] salt, List<byte[]> moduleIds, CancellationTokenSource cancellationToken = null)
        {
            var createAccountIfNeededFunction = new CreateAccountIfNeededFunction();
                createAccountIfNeededFunction.Owner = owner;
                createAccountIfNeededFunction.Salt = salt;
                createAccountIfNeededFunction.ModuleIds = moduleIds;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountIfNeededFunction, cancellationToken);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public Task<string> GetAddressQueryAsync(GetAddressFunction getAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        
        public virtual Task<string> GetAddressQueryAsync(string owner, byte[] salt, List<byte[]> moduleIds, BlockParameter blockParameter = null)
        {
            var getAddressFunction = new GetAddressFunction();
                getAddressFunction.Owner = owner;
                getAddressFunction.Salt = salt;
                getAddressFunction.ModuleIds = moduleIds;
            
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        public Task<BigInteger> GetAdminCountQueryAsync(GetAdminCountFunction getAdminCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAdminCountFunction, BigInteger>(getAdminCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetAdminCountQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAdminCountFunction, BigInteger>(null, blockParameter);
        }

        public Task<List<string>> GetAdminsQueryAsync(GetAdminsFunction getAdminsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAdminsFunction, List<string>>(getAdminsFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetAdminsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAdminsFunction, List<string>>(null, blockParameter);
        }

        public Task<byte[]> GetDomainSeparatorQueryAsync(GetDomainSeparatorFunction getDomainSeparatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDomainSeparatorFunction, byte[]>(getDomainSeparatorFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetDomainSeparatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDomainSeparatorFunction, byte[]>(null, blockParameter);
        }

        public Task<string> GetModuleAddressQueryAsync(GetModuleAddressFunction getModuleAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetModuleAddressFunction, string>(getModuleAddressFunction, blockParameter);
        }

        
        public virtual Task<string> GetModuleAddressQueryAsync(byte[] moduleId, BlockParameter blockParameter = null)
        {
            var getModuleAddressFunction = new GetModuleAddressFunction();
                getModuleAddressFunction.ModuleId = moduleId;
            
            return ContractHandler.QueryAsync<GetModuleAddressFunction, string>(getModuleAddressFunction, blockParameter);
        }

        public Task<BigInteger> GetRegisteredModuleCountQueryAsync(GetRegisteredModuleCountFunction getRegisteredModuleCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRegisteredModuleCountFunction, BigInteger>(getRegisteredModuleCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRegisteredModuleCountQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRegisteredModuleCountFunction, BigInteger>(null, blockParameter);
        }

        public Task<List<byte[]>> GetRegisteredModulesQueryAsync(GetRegisteredModulesFunction getRegisteredModulesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRegisteredModulesFunction, List<byte[]>>(getRegisteredModulesFunction, blockParameter);
        }

        
        public virtual Task<List<byte[]>> GetRegisteredModulesQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRegisteredModulesFunction, List<byte[]>>(null, blockParameter);
        }

        public Task<string> ImplementationQueryAsync(ImplementationFunction implementationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ImplementationFunction, string>(implementationFunction, blockParameter);
        }

        
        public virtual Task<string> ImplementationQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ImplementationFunction, string>(null, blockParameter);
        }

        public Task<bool> IsAdminQueryAsync(IsAdminFunction isAdminFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsAdminFunction, bool>(isAdminFunction, blockParameter);
        }

        
        public virtual Task<bool> IsAdminQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var isAdminFunction = new IsAdminFunction();
                isAdminFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsAdminFunction, bool>(isAdminFunction, blockParameter);
        }

        public Task<bool> IsDeployedQueryAsync(IsDeployedFunction isDeployedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsDeployedFunction, bool>(isDeployedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsDeployedQueryAsync(string owner, byte[] salt, List<byte[]> moduleIds, BlockParameter blockParameter = null)
        {
            var isDeployedFunction = new IsDeployedFunction();
                isDeployedFunction.Owner = owner;
                isDeployedFunction.Salt = salt;
                isDeployedFunction.ModuleIds = moduleIds;
            
            return ContractHandler.QueryAsync<IsDeployedFunction, bool>(isDeployedFunction, blockParameter);
        }

        public Task<BigInteger> NonceQueryAsync(NonceFunction nonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(nonceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NonceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NonceFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> RegisterModuleRequestAsync(RegisterModuleFunction registerModuleFunction)
        {
             return ContractHandler.SendRequestAsync(registerModuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterModuleRequestAndWaitForReceiptAsync(RegisterModuleFunction registerModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerModuleFunction, cancellationToken);
        }

        public virtual Task<string> RegisterModuleRequestAsync(byte[] moduleId, string moduleAddress, BigInteger deadline, List<byte[]> signatures)
        {
            var registerModuleFunction = new RegisterModuleFunction();
                registerModuleFunction.ModuleId = moduleId;
                registerModuleFunction.ModuleAddress = moduleAddress;
                registerModuleFunction.Deadline = deadline;
                registerModuleFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAsync(registerModuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterModuleRequestAndWaitForReceiptAsync(byte[] moduleId, string moduleAddress, BigInteger deadline, List<byte[]> signatures, CancellationTokenSource cancellationToken = null)
        {
            var registerModuleFunction = new RegisterModuleFunction();
                registerModuleFunction.ModuleId = moduleId;
                registerModuleFunction.ModuleAddress = moduleAddress;
                registerModuleFunction.Deadline = deadline;
                registerModuleFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerModuleFunction, cancellationToken);
        }

        public Task<BigInteger> ThresholdQueryAsync(ThresholdFunction thresholdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(thresholdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ThresholdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ThresholdFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> UnregisterModuleRequestAsync(UnregisterModuleFunction unregisterModuleFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterModuleFunction);
        }

        public virtual Task<TransactionReceipt> UnregisterModuleRequestAndWaitForReceiptAsync(UnregisterModuleFunction unregisterModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterModuleFunction, cancellationToken);
        }

        public virtual Task<string> UnregisterModuleRequestAsync(byte[] moduleId, BigInteger deadline, List<byte[]> signatures)
        {
            var unregisterModuleFunction = new UnregisterModuleFunction();
                unregisterModuleFunction.ModuleId = moduleId;
                unregisterModuleFunction.Deadline = deadline;
                unregisterModuleFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAsync(unregisterModuleFunction);
        }

        public virtual Task<TransactionReceipt> UnregisterModuleRequestAndWaitForReceiptAsync(byte[] moduleId, BigInteger deadline, List<byte[]> signatures, CancellationTokenSource cancellationToken = null)
        {
            var unregisterModuleFunction = new UnregisterModuleFunction();
                unregisterModuleFunction.ModuleId = moduleId;
                unregisterModuleFunction.Deadline = deadline;
                unregisterModuleFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterModuleFunction, cancellationToken);
        }

        public virtual Task<string> UpdateAdminsRequestAsync(UpdateAdminsFunction updateAdminsFunction)
        {
             return ContractHandler.SendRequestAsync(updateAdminsFunction);
        }

        public virtual Task<TransactionReceipt> UpdateAdminsRequestAndWaitForReceiptAsync(UpdateAdminsFunction updateAdminsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateAdminsFunction, cancellationToken);
        }

        public virtual Task<string> UpdateAdminsRequestAsync(List<string> newAdmins, BigInteger newThreshold, BigInteger deadline, List<byte[]> signatures)
        {
            var updateAdminsFunction = new UpdateAdminsFunction();
                updateAdminsFunction.NewAdmins = newAdmins;
                updateAdminsFunction.NewThreshold = newThreshold;
                updateAdminsFunction.Deadline = deadline;
                updateAdminsFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAsync(updateAdminsFunction);
        }

        public virtual Task<TransactionReceipt> UpdateAdminsRequestAndWaitForReceiptAsync(List<string> newAdmins, BigInteger newThreshold, BigInteger deadline, List<byte[]> signatures, CancellationTokenSource cancellationToken = null)
        {
            var updateAdminsFunction = new UpdateAdminsFunction();
                updateAdminsFunction.NewAdmins = newAdmins;
                updateAdminsFunction.NewThreshold = newThreshold;
                updateAdminsFunction.Deadline = deadline;
                updateAdminsFunction.Signatures = signatures;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateAdminsFunction, cancellationToken);
        }

        public Task<bool> ValidateSignaturesQueryAsync(ValidateSignaturesFunction validateSignaturesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateSignaturesFunction, bool>(validateSignaturesFunction, blockParameter);
        }

        
        public virtual Task<bool> ValidateSignaturesQueryAsync(byte[] digest, List<byte[]> signatures, BlockParameter blockParameter = null)
        {
            var validateSignaturesFunction = new ValidateSignaturesFunction();
                validateSignaturesFunction.Digest = digest;
                validateSignaturesFunction.Signatures = signatures;
            
            return ContractHandler.QueryAsync<ValidateSignaturesFunction, bool>(validateSignaturesFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MaxAdminsFunction),
                typeof(AccountRegistryFunction),
                typeof(CreateAccountFunction),
                typeof(CreateAccountIfNeededFunction),
                typeof(EntryPointFunction),
                typeof(GetAddressFunction),
                typeof(GetAdminCountFunction),
                typeof(GetAdminsFunction),
                typeof(GetDomainSeparatorFunction),
                typeof(GetModuleAddressFunction),
                typeof(GetRegisteredModuleCountFunction),
                typeof(GetRegisteredModulesFunction),
                typeof(ImplementationFunction),
                typeof(IsAdminFunction),
                typeof(IsDeployedFunction),
                typeof(NonceFunction),
                typeof(RegisterModuleFunction),
                typeof(ThresholdFunction),
                typeof(UnregisterModuleFunction),
                typeof(UpdateAdminsFunction),
                typeof(ValidateSignaturesFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountCreatedEventDTO),
                typeof(AdminsUpdatedEventDTO),
                typeof(ModuleRegisteredEventDTO),
                typeof(ModuleUnregisteredEventDTO)
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
