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
using Nethereum.AccountAbstraction.Contracts.Core.SmartAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.SmartAccountFactory
{
    public partial class SmartAccountFactoryService: SmartAccountFactoryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SmartAccountFactoryDeployment smartAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SmartAccountFactoryDeployment>().SendRequestAndWaitForReceiptAsync(smartAccountFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SmartAccountFactoryDeployment smartAccountFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SmartAccountFactoryDeployment>().SendRequestAsync(smartAccountFactoryDeployment);
        }

        public static async Task<SmartAccountFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SmartAccountFactoryDeployment smartAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, smartAccountFactoryDeployment, cancellationTokenSource);
            return new SmartAccountFactoryService(web3, receipt.ContractAddress);
        }

        public SmartAccountFactoryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SmartAccountFactoryServiceBase: ContractWeb3ServiceBase
    {

        public SmartAccountFactoryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
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

        public Task<string> ModuleRegistryQueryAsync(ModuleRegistryFunction moduleRegistryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ModuleRegistryFunction, string>(moduleRegistryFunction, blockParameter);
        }

        
        public virtual Task<string> ModuleRegistryQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var moduleRegistryFunction = new ModuleRegistryFunction();
                moduleRegistryFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ModuleRegistryFunction, string>(moduleRegistryFunction, blockParameter);
        }

        public virtual Task<string> RegisterModuleRequestAsync(RegisterModuleFunction registerModuleFunction)
        {
             return ContractHandler.SendRequestAsync(registerModuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterModuleRequestAndWaitForReceiptAsync(RegisterModuleFunction registerModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerModuleFunction, cancellationToken);
        }

        public virtual Task<string> RegisterModuleRequestAsync(byte[] moduleId, string moduleAddress)
        {
            var registerModuleFunction = new RegisterModuleFunction();
                registerModuleFunction.ModuleId = moduleId;
                registerModuleFunction.ModuleAddress = moduleAddress;
            
             return ContractHandler.SendRequestAsync(registerModuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterModuleRequestAndWaitForReceiptAsync(byte[] moduleId, string moduleAddress, CancellationTokenSource cancellationToken = null)
        {
            var registerModuleFunction = new RegisterModuleFunction();
                registerModuleFunction.ModuleId = moduleId;
                registerModuleFunction.ModuleAddress = moduleAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerModuleFunction, cancellationToken);
        }

        public Task<byte[]> RegisteredModuleIdsQueryAsync(RegisteredModuleIdsFunction registeredModuleIdsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegisteredModuleIdsFunction, byte[]>(registeredModuleIdsFunction, blockParameter);
        }

        
        public virtual Task<byte[]> RegisteredModuleIdsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var registeredModuleIdsFunction = new RegisteredModuleIdsFunction();
                registeredModuleIdsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<RegisteredModuleIdsFunction, byte[]>(registeredModuleIdsFunction, blockParameter);
        }

        public virtual Task<string> UnregisterModuleRequestAsync(UnregisterModuleFunction unregisterModuleFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterModuleFunction);
        }

        public virtual Task<TransactionReceipt> UnregisterModuleRequestAndWaitForReceiptAsync(UnregisterModuleFunction unregisterModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterModuleFunction, cancellationToken);
        }

        public virtual Task<string> UnregisterModuleRequestAsync(byte[] moduleId)
        {
            var unregisterModuleFunction = new UnregisterModuleFunction();
                unregisterModuleFunction.ModuleId = moduleId;
            
             return ContractHandler.SendRequestAsync(unregisterModuleFunction);
        }

        public virtual Task<TransactionReceipt> UnregisterModuleRequestAndWaitForReceiptAsync(byte[] moduleId, CancellationTokenSource cancellationToken = null)
        {
            var unregisterModuleFunction = new UnregisterModuleFunction();
                unregisterModuleFunction.ModuleId = moduleId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterModuleFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountRegistryFunction),
                typeof(CreateAccountFunction),
                typeof(CreateAccountIfNeededFunction),
                typeof(EntryPointFunction),
                typeof(GetAddressFunction),
                typeof(GetModuleAddressFunction),
                typeof(GetRegisteredModulesFunction),
                typeof(ImplementationFunction),
                typeof(IsDeployedFunction),
                typeof(ModuleRegistryFunction),
                typeof(RegisterModuleFunction),
                typeof(RegisteredModuleIdsFunction),
                typeof(UnregisterModuleFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountCreatedEventDTO),
                typeof(ModuleRegisteredEventDTO),
                typeof(ModuleUnregisteredEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AccountAlreadyDeployedError),
                typeof(FailedDeploymentError),
                typeof(InsufficientBalanceError),
                typeof(InvalidModuleAddressError),
                typeof(ModuleAlreadyRegisteredError),
                typeof(ModuleNotRegisteredError)
            };
        }
    }
}
