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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.HookMultiPlexer.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.HookMultiPlexer
{
    public partial class HookMultiPlexerService: HookMultiPlexerServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, HookMultiPlexerDeployment hookMultiPlexerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<HookMultiPlexerDeployment>().SendRequestAndWaitForReceiptAsync(hookMultiPlexerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, HookMultiPlexerDeployment hookMultiPlexerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<HookMultiPlexerDeployment>().SendRequestAsync(hookMultiPlexerDeployment);
        }

        public static async Task<HookMultiPlexerService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, HookMultiPlexerDeployment hookMultiPlexerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, hookMultiPlexerDeployment, cancellationTokenSource);
            return new HookMultiPlexerService(web3, receipt.ContractAddress);
        }

        public HookMultiPlexerService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class HookMultiPlexerServiceBase: ContractWeb3ServiceBase
    {

        public HookMultiPlexerServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public virtual Task<string> AddHookRequestAsync(AddHookFunction addHookFunction)
        {
             return ContractHandler.SendRequestAsync(addHookFunction);
        }

        public virtual Task<TransactionReceipt> AddHookRequestAndWaitForReceiptAsync(AddHookFunction addHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addHookFunction, cancellationToken);
        }

        public virtual Task<string> AddHookRequestAsync(string hook, byte hookType)
        {
            var addHookFunction = new AddHookFunction();
                addHookFunction.Hook = hook;
                addHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAsync(addHookFunction);
        }

        public virtual Task<TransactionReceipt> AddHookRequestAndWaitForReceiptAsync(string hook, byte hookType, CancellationTokenSource cancellationToken = null)
        {
            var addHookFunction = new AddHookFunction();
                addHookFunction.Hook = hook;
                addHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addHookFunction, cancellationToken);
        }

        public virtual Task<string> AddSigHookRequestAsync(AddSigHookFunction addSigHookFunction)
        {
             return ContractHandler.SendRequestAsync(addSigHookFunction);
        }

        public virtual Task<TransactionReceipt> AddSigHookRequestAndWaitForReceiptAsync(AddSigHookFunction addSigHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addSigHookFunction, cancellationToken);
        }

        public virtual Task<string> AddSigHookRequestAsync(string hook, byte[] sig, byte hookType)
        {
            var addSigHookFunction = new AddSigHookFunction();
                addSigHookFunction.Hook = hook;
                addSigHookFunction.Sig = sig;
                addSigHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAsync(addSigHookFunction);
        }

        public virtual Task<TransactionReceipt> AddSigHookRequestAndWaitForReceiptAsync(string hook, byte[] sig, byte hookType, CancellationTokenSource cancellationToken = null)
        {
            var addSigHookFunction = new AddSigHookFunction();
                addSigHookFunction.Hook = hook;
                addSigHookFunction.Sig = sig;
                addSigHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addSigHookFunction, cancellationToken);
        }

        public Task<List<string>> GetHooksQueryAsync(GetHooksFunction getHooksFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetHooksFunction, List<string>>(getHooksFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetHooksQueryAsync(string smartAccount, BlockParameter blockParameter = null)
        {
            var getHooksFunction = new GetHooksFunction();
                getHooksFunction.SmartAccount = smartAccount;
            
            return ContractHandler.QueryAsync<GetHooksFunction, List<string>>(getHooksFunction, blockParameter);
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

        public virtual Task<string> RemoveHookRequestAsync(RemoveHookFunction removeHookFunction)
        {
             return ContractHandler.SendRequestAsync(removeHookFunction);
        }

        public virtual Task<TransactionReceipt> RemoveHookRequestAndWaitForReceiptAsync(RemoveHookFunction removeHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeHookFunction, cancellationToken);
        }

        public virtual Task<string> RemoveHookRequestAsync(string hook, byte hookType)
        {
            var removeHookFunction = new RemoveHookFunction();
                removeHookFunction.Hook = hook;
                removeHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAsync(removeHookFunction);
        }

        public virtual Task<TransactionReceipt> RemoveHookRequestAndWaitForReceiptAsync(string hook, byte hookType, CancellationTokenSource cancellationToken = null)
        {
            var removeHookFunction = new RemoveHookFunction();
                removeHookFunction.Hook = hook;
                removeHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeHookFunction, cancellationToken);
        }

        public virtual Task<string> RemoveSigHookRequestAsync(RemoveSigHookFunction removeSigHookFunction)
        {
             return ContractHandler.SendRequestAsync(removeSigHookFunction);
        }

        public virtual Task<TransactionReceipt> RemoveSigHookRequestAndWaitForReceiptAsync(RemoveSigHookFunction removeSigHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeSigHookFunction, cancellationToken);
        }

        public virtual Task<string> RemoveSigHookRequestAsync(string hook, byte[] sig, byte hookType)
        {
            var removeSigHookFunction = new RemoveSigHookFunction();
                removeSigHookFunction.Hook = hook;
                removeSigHookFunction.Sig = sig;
                removeSigHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAsync(removeSigHookFunction);
        }

        public virtual Task<TransactionReceipt> RemoveSigHookRequestAndWaitForReceiptAsync(string hook, byte[] sig, byte hookType, CancellationTokenSource cancellationToken = null)
        {
            var removeSigHookFunction = new RemoveSigHookFunction();
                removeSigHookFunction.Hook = hook;
                removeSigHookFunction.Sig = sig;
                removeSigHookFunction.HookType = hookType;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeSigHookFunction, cancellationToken);
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
                typeof(AddHookFunction),
                typeof(AddSigHookFunction),
                typeof(GetHooksFunction),
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(NameFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(PostCheckFunction),
                typeof(PreCheckFunction),
                typeof(RemoveHookFunction),
                typeof(RemoveSigHookFunction),
                typeof(VersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountInitializedEventDTO),
                typeof(AccountUninitializedEventDTO),
                typeof(HookAddedEventDTO),
                typeof(HookRemovedEventDTO),
                typeof(SigHookAddedEventDTO),
                typeof(SigHookRemovedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(HooksNotSortedError),
                typeof(ModuleAlreadyInitializedError),
                typeof(NotInitializedError),
                typeof(SubHookPostCheckErrorError),
                typeof(SubHookPreCheckErrorError),
                typeof(UnsupportedHookTypeError)
            };
        }
    }
}
