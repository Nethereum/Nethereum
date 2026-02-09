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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.RegistryHook.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.RegistryHook
{
    public partial class RegistryHookService: RegistryHookServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, RegistryHookDeployment registryHookDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistryHookDeployment>().SendRequestAndWaitForReceiptAsync(registryHookDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, RegistryHookDeployment registryHookDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistryHookDeployment>().SendRequestAsync(registryHookDeployment);
        }

        public static async Task<RegistryHookService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, RegistryHookDeployment registryHookDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, registryHookDeployment, cancellationTokenSource);
            return new RegistryHookService(web3, receipt.ContractAddress);
        }

        public RegistryHookService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class RegistryHookServiceBase: ContractWeb3ServiceBase
    {

        public RegistryHookServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> ClearTrustedForwarderRequestAsync(ClearTrustedForwarderFunction clearTrustedForwarderFunction)
        {
             return ContractHandler.SendRequestAsync(clearTrustedForwarderFunction);
        }

        public virtual Task<string> ClearTrustedForwarderRequestAsync()
        {
             return ContractHandler.SendRequestAsync<ClearTrustedForwarderFunction>();
        }

        public virtual Task<TransactionReceipt> ClearTrustedForwarderRequestAndWaitForReceiptAsync(ClearTrustedForwarderFunction clearTrustedForwarderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(clearTrustedForwarderFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> ClearTrustedForwarderRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<ClearTrustedForwarderFunction>(null, cancellationToken);
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

        public Task<bool> IsTrustedForwarderQueryAsync(IsTrustedForwarderFunction isTrustedForwarderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsTrustedForwarderFunction, bool>(isTrustedForwarderFunction, blockParameter);
        }

        
        public virtual Task<bool> IsTrustedForwarderQueryAsync(string forwarder, string account, BlockParameter blockParameter = null)
        {
            var isTrustedForwarderFunction = new IsTrustedForwarderFunction();
                isTrustedForwarderFunction.Forwarder = forwarder;
                isTrustedForwarderFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsTrustedForwarderFunction, bool>(isTrustedForwarderFunction, blockParameter);
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

        public virtual Task<string> PostCheckRequestAsync(PostCheckFunction postCheckFunction)
        {
             return ContractHandler.SendRequestAsync(postCheckFunction);
        }

        public virtual Task<TransactionReceipt> PostCheckRequestAndWaitForReceiptAsync(PostCheckFunction postCheckFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postCheckFunction, cancellationToken);
        }

        public virtual Task<string> PostCheckRequestAsync(byte[] hookData)
        {
            var postCheckFunction = new PostCheckFunction();
                postCheckFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAsync(postCheckFunction);
        }

        public virtual Task<TransactionReceipt> PostCheckRequestAndWaitForReceiptAsync(byte[] hookData, CancellationTokenSource cancellationToken = null)
        {
            var postCheckFunction = new PostCheckFunction();
                postCheckFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postCheckFunction, cancellationToken);
        }

        public virtual Task<string> PreCheckRequestAsync(PreCheckFunction preCheckFunction)
        {
             return ContractHandler.SendRequestAsync(preCheckFunction);
        }

        public virtual Task<TransactionReceipt> PreCheckRequestAndWaitForReceiptAsync(PreCheckFunction preCheckFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preCheckFunction, cancellationToken);
        }

        public virtual Task<string> PreCheckRequestAsync(string msgSender, BigInteger msgValue, byte[] msgData)
        {
            var preCheckFunction = new PreCheckFunction();
                preCheckFunction.MsgSender = msgSender;
                preCheckFunction.MsgValue = msgValue;
                preCheckFunction.MsgData = msgData;
            
             return ContractHandler.SendRequestAsync(preCheckFunction);
        }

        public virtual Task<TransactionReceipt> PreCheckRequestAndWaitForReceiptAsync(string msgSender, BigInteger msgValue, byte[] msgData, CancellationTokenSource cancellationToken = null)
        {
            var preCheckFunction = new PreCheckFunction();
                preCheckFunction.MsgSender = msgSender;
                preCheckFunction.MsgValue = msgValue;
                preCheckFunction.MsgData = msgData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(preCheckFunction, cancellationToken);
        }

        public Task<string> RegistryQueryAsync(RegistryFunction registryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryFunction, string>(registryFunction, blockParameter);
        }

        
        public virtual Task<string> RegistryQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var registryFunction = new RegistryFunction();
                registryFunction.Account = account;
            
            return ContractHandler.QueryAsync<RegistryFunction, string>(registryFunction, blockParameter);
        }

        public virtual Task<string> SetRegistryRequestAsync(SetRegistryFunction setRegistryFunction)
        {
             return ContractHandler.SendRequestAsync(setRegistryFunction);
        }

        public virtual Task<TransactionReceipt> SetRegistryRequestAndWaitForReceiptAsync(SetRegistryFunction setRegistryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistryFunction, cancellationToken);
        }

        public virtual Task<string> SetRegistryRequestAsync(string registry)
        {
            var setRegistryFunction = new SetRegistryFunction();
                setRegistryFunction.Registry = registry;
            
             return ContractHandler.SendRequestAsync(setRegistryFunction);
        }

        public virtual Task<TransactionReceipt> SetRegistryRequestAndWaitForReceiptAsync(string registry, CancellationTokenSource cancellationToken = null)
        {
            var setRegistryFunction = new SetRegistryFunction();
                setRegistryFunction.Registry = registry;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRegistryFunction, cancellationToken);
        }

        public virtual Task<string> SetTrustedForwarderRequestAsync(SetTrustedForwarderFunction setTrustedForwarderFunction)
        {
             return ContractHandler.SendRequestAsync(setTrustedForwarderFunction);
        }

        public virtual Task<TransactionReceipt> SetTrustedForwarderRequestAndWaitForReceiptAsync(SetTrustedForwarderFunction setTrustedForwarderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTrustedForwarderFunction, cancellationToken);
        }

        public virtual Task<string> SetTrustedForwarderRequestAsync(string forwarder)
        {
            var setTrustedForwarderFunction = new SetTrustedForwarderFunction();
                setTrustedForwarderFunction.Forwarder = forwarder;
            
             return ContractHandler.SendRequestAsync(setTrustedForwarderFunction);
        }

        public virtual Task<TransactionReceipt> SetTrustedForwarderRequestAndWaitForReceiptAsync(string forwarder, CancellationTokenSource cancellationToken = null)
        {
            var setTrustedForwarderFunction = new SetTrustedForwarderFunction();
                setTrustedForwarderFunction.Forwarder = forwarder;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTrustedForwarderFunction, cancellationToken);
        }

        public Task<string> TrustedForwarderQueryAsync(TrustedForwarderFunction trustedForwarderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TrustedForwarderFunction, string>(trustedForwarderFunction, blockParameter);
        }

        
        public virtual Task<string> TrustedForwarderQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var trustedForwarderFunction = new TrustedForwarderFunction();
                trustedForwarderFunction.Account = account;
            
            return ContractHandler.QueryAsync<TrustedForwarderFunction, string>(trustedForwarderFunction, blockParameter);
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
                typeof(ClearTrustedForwarderFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(IsTrustedForwarderFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(PostCheckFunction),
                typeof(PreCheckFunction),
                typeof(RegistryFunction),
                typeof(SetRegistryFunction),
                typeof(SetTrustedForwarderFunction),
                typeof(TrustedForwarderFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(RegistrySetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(HookInvalidSelectorError),
                typeof(InvalidCallTypeError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError)
            };
        }
    }
}
