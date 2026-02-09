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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IHook.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IHook
{
    public partial class IHookService: IHookServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IHookDeployment iHookDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IHookDeployment>().SendRequestAndWaitForReceiptAsync(iHookDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IHookDeployment iHookDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IHookDeployment>().SendRequestAsync(iHookDeployment);
        }

        public static async Task<IHookService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IHookDeployment iHookDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iHookDeployment, cancellationTokenSource);
            return new IHookService(web3, receipt.ContractAddress);
        }

        public IHookService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IHookServiceBase: ContractWeb3ServiceBase
    {

        public IHookServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
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

        public virtual Task<string> OnUninstallRequestAsync(byte[] data)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(onUninstallFunction);
        }

        public virtual Task<TransactionReceipt> OnUninstallRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var onUninstallFunction = new OnUninstallFunction();
                onUninstallFunction.Data = data;
            
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(IsInitializedFunction),
                typeof(IsModuleTypeFunction),
                typeof(OnInstallFunction),
                typeof(OnUninstallFunction),
                typeof(PostCheckFunction),
                typeof(PreCheckFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

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
